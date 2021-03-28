// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Common;
using Common.Extensions;
using static Common.Log;
using static Common.Util;

namespace Server.Database.Updater
{
    using LocaleFileStorage = SortedSet<LocaleFileEntry>;
    using HashToFileNameStorage = Dictionary<string, string>;
    using AppliedFileStorage = Dictionary<string, AppliedFileEntry>;
    using DirectoryStorage = List<DirectoryEntry>;

    public struct UpdateResult
    {
        public UpdateResult(int updated, int recent, int archived)
        {
            Updated = updated;
            Recent = recent;
            Archived = archived;
        }

        public readonly int Updated;
        public readonly int Recent;
        public readonly int Archived;
    }

    public enum UpdateMode
    {
        MODE_APPLY,
        MODE_REHASH
    };

    public enum State
    {
        RELEASED,
        ARCHIVED
    };

    public struct AppliedFileEntry
    {
        public AppliedFileEntry(string name, string hash, State state, uint timestamp)
        {
            Name = name;
            Hash = hash;
            State = state;
            Timestamp = timestamp;
        }

        public readonly string Name;
        public readonly string Hash;
        public readonly State State;
        public readonly uint Timestamp;
    }

    public struct LocaleFileEntry : IComparable<LocaleFileEntry>
    {
        public LocaleFileEntry(string path, State state)
        {
            FilePath = path;
            State = state;
        }

        public int CompareTo(LocaleFileEntry other)
        {
            return GetFileName().CompareTo(other.GetFileName());
        }

        public string GetFileName()
        {
            return Path.GetFileName(FilePath);
        }

        public readonly string FilePath;
        public readonly State State;
    }

    public struct DirectoryEntry
    {
        public DirectoryEntry(string path, State state)
        {
            DirectoryPath = path;
            State = state;
        }

        public readonly string DirectoryPath;
        public readonly State State;
    }

    public class UpdateFetcher
    {
        public UpdateFetcher(string updateDirectory, Action<string> apply, Action<string> applyFile, Func<string, QueryResult?> retrieve)
        {
            _sourceDirectory = updateDirectory;
            _apply = apply;
            _applyFile = applyFile;
            _retrieve = retrieve;
        }

        public UpdateResult Update(bool redundancyChecks, bool allowRehash, bool archivedRedundancy, int cleanDeadReferencesMaxCount)
        {
            var available = GetFileList();
            AppliedFileStorage applied = ReceiveAppliedFiles();

            int countRecentUpdates = 0;
            int countArchivedUpdates = 0;

            // Count updates
            foreach (var entry in applied.Values)
                if (entry.State == State.RELEASED)
                    ++countRecentUpdates;
                else
                    ++countArchivedUpdates;

            // Fill hash to name cache
            HashToFileNameStorage hashToName = new HashToFileNameStorage();
            foreach (var entry in applied)
                hashToName[entry.Value.Hash] = entry.Key;

            int importedUpdates = 0;

            foreach (var availableQuery in available)
            {
                var filename = availableQuery.GetFileName();
                FEL_LOG_DEBUG("sql.updates", "Checking update \"{0}\"...", filename);

                AppliedFileEntry appliedFile;
                bool filenameApplied = applied.TryGetValue(filename, out appliedFile);

                if (filenameApplied)
                {
                    // If redundancy is disabled, skip it, because the update is already applied.
                    if (!redundancyChecks)
                    {
                        FEL_LOG_DEBUG("sql.updates", ">> Update is already applied, skipping redundancy checks.");
                        applied.Remove(filename);
                        continue;
                    }

                    // If the update is in an archived directory and is marked as archived in our database, skip redundancy checks (archived updates never change).
                    if (!archivedRedundancy && (appliedFile.State == State.ARCHIVED) && (availableQuery.State == State.ARCHIVED))
                    {
                        FEL_LOG_DEBUG("sql.updates", ">> Update is archived and marked as archived in database, skipping redundancy checks.");
                        applied.Remove(filename);
                        continue;
                    }
                }

                // Calculate a Sha1 hash based on query content.
                string hash = ByteArrayToHexStr(DigestSHA1(ReadSQLUpdate(availableQuery.FilePath)));

                UpdateMode mode = UpdateMode.MODE_APPLY;

                // Update is not in our applied list
                if (!filenameApplied)
                {
                    // Catch renames (different filename, but same hash)
                    if (hashToName.TryGetValue(hash, out filename))
                    {
                        // Check if the original file was removed. If not, we've got a problem.
                        LocaleFileEntry? renameFile = null;
                        foreach (var item in available)
                        {
                            if (item.GetFileName() == filename)
                            {
                                renameFile = item;
                                break;
                            }
                        }

                        // Conflict!
                        if (renameFile != null)
                        {
                            FEL_LOG_WARN("sql.updates", ">> It seems like the update \"{0}\" \'{1}\' was renamed, but the old file is still there! Treating it as a new file! (It is probably an unmodified copy of the file \"{2}\")",
                                    availableQuery.GetFileName(), hash.Substring(0, 7), renameFile.Value.GetFileName());
                        }
                        // It is safe to treat the file as renamed here
                        else
                        {
                            FEL_LOG_INFO("sql.updates", ">> Renaming update \"{0}\" to \"{1}\" \'{2}\'.", filename, availableQuery.GetFileName(), hash.Substring(0, 7));

                            RenameEntry(filename, availableQuery.GetFileName());
                            applied.Remove(filename);
                            continue;
                        }
                    }
                    // Apply the update if it was never seen before.
                    else
                    {
                        FEL_LOG_INFO("sql.updates", ">> Applying update \"{0}\" \'{1}\'...", availableQuery.GetFileName(), hash.Substring(0, 7));
                    }
                }
                // Rehash the update entry if it exists in our database with an empty hash.
                else if (allowRehash && string.IsNullOrEmpty(appliedFile.Hash))
                {
                    mode = UpdateMode.MODE_REHASH;

                    FEL_LOG_INFO("sql.updates", ">> Re-hashing update \"{0}\" \'{1}\'...", availableQuery.GetFileName(), hash.Substring(0, 7));
                }
                else
                {
                    // If the hash of the files differs from the one stored in our database, reapply the update (because it changed).
                    if (appliedFile.Hash != hash)
                    {
                        FEL_LOG_INFO("sql.updates", ">> Reapplying update \"{0}\" \'{1}\' -> \'{2}\' (it changed)...", availableQuery.GetFileName(),
                            appliedFile.Hash.Substring(0, 7), hash.Substring(0, 7));
                    }
                    else
                    {
                        // If the file wasn't changed and just moved, update its state (if necessary).
                        if (appliedFile.State != availableQuery.State)
                        {
                            FEL_LOG_DEBUG("sql.updates", ">> Updating the state of \"{0}\" to \'{1}\'...", availableQuery.GetFileName(), availableQuery.State);

                            UpdateState(availableQuery.GetFileName(), availableQuery.State);
                        }

                        FEL_LOG_DEBUG("sql.updates", ">> Update is already applied and matches the hash \'{0}\'.", hash.Substring(0, 7));

                        applied.Remove(appliedFile.Name);
                        continue;
                    }
                }

                uint speed = 0;
                var file = new AppliedFileEntry(availableQuery.GetFileName(), hash, availableQuery.State, 0);

                switch (mode)
                {
                    case UpdateMode.MODE_APPLY:
                        speed = Apply(availableQuery.FilePath);
                        goto case UpdateMode.MODE_REHASH;
                    case UpdateMode.MODE_REHASH:
                        UpdateEntry(ref file, speed);
                        break;
                }

                if (filenameApplied)
                    applied.Remove(appliedFile.Name);

                if (mode == UpdateMode.MODE_APPLY)
                    ++importedUpdates;
            }

            // Cleanup up orphaned entries (if enabled)
            if (applied.Count > 0)
            {
                bool doCleanup = (cleanDeadReferencesMaxCount < 0) || (applied.Count <= cleanDeadReferencesMaxCount);

                foreach (var entry in applied)
                {
                    FEL_LOG_WARN("sql.updates", ">> The file \'{0}\' was applied to the database, but is missing in your update directory now!", entry.Key);

                    if (doCleanup)
                        FEL_LOG_INFO("sql.updates", "Deleting orphaned entry \'{0}\'...", entry.Key);
                }

                if (doCleanup)
                    CleanUp(applied);
                else
                {
                    FEL_LOG_ERROR("sql.updates", "Cleanup is disabled! There were {0} dirty files applied to your database, but they are now missing in your source directory!", applied.Count);
                }
            }

            return new UpdateResult(importedUpdates, countRecentUpdates, countArchivedUpdates);
        }

        LocaleFileStorage GetFileList()
        {
            LocaleFileStorage files = new LocaleFileStorage();
            DirectoryStorage directories = ReceiveIncludedDirectories();
            foreach (var entry in directories)
                FillFileListRecursively(entry.DirectoryPath, files, entry.State, 1);

            return files;
        }

        const int MAX_DEPTH = 10;
        void FillFileListRecursively(string path, LocaleFileStorage storage, State state, int depth)
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                if (Directory.Exists(file))
                {
                    if (depth < MAX_DEPTH)
                        FillFileListRecursively(file, storage, state, depth + 1);
                }
                else if (Path.GetExtension(file) == ".sql")
                {
                    FEL_LOG_TRACE("sql.updates", "Added locale file \"{0}\".", Path.GetFileName(file));

                    var entry = new LocaleFileEntry(file, state);

                    // Check for doubled filenames
                    // Because elements are only compared by their filenames, this is ok
                    if (storage.Contains(entry))
                    {
                        FEL_LOG_FATAL("sql.updates", "Duplicate filename \"{0}\" occurred. Because updates are ordered by their filenames, every name needs to be unique!", file);

                        throw new UpdateException("Updating failed, see the log for details.");
                    }

                    storage.Add(entry);
                }
            }
        }

        DirectoryStorage ReceiveIncludedDirectories()
        {
            DirectoryStorage directories = new DirectoryStorage();

            var result = _retrieve("SELECT `path`, `state` FROM `updates_include`");
            if (result == null)
                return directories;

            do
            {
                var reader = result.Reader;

                var path = reader.GetString(0);
                if (path.Substring(0, 1) == "$")
                    path = Path.Combine(_sourceDirectory, path.Substring(1));

                if (!Directory.Exists(path))
                {
                    FEL_LOG_WARN("sql.updates", "DBUpdater: Given update include directory \"{0}\" does not exist, skipped!", path);
                    continue;
                }

                directories.Add(new DirectoryEntry(path, reader.GetString(1).ToEnum<State>()));

                FEL_LOG_TRACE("sql.updates", "Added applied file \"{0}\" from remote.", Path.GetFileName(path));

            } while (result.NextRow());

            result.Dispose();

            return directories;
        }

        AppliedFileStorage ReceiveAppliedFiles()
        {
            var map = new Dictionary<string, AppliedFileEntry>();

            var result = _retrieve("SELECT `name`, `hash`, `state`, UNIX_TIMESTAMP(`timestamp`) FROM `updates` ORDER BY `name` ASC");
            if (result == null)
                return map;

            do
            {
                var reader = result.Reader;
                var entry = new AppliedFileEntry(reader.GetString(0), reader.GetString(1),
                    reader.GetString(2).ToEnum<State>(), reader.GetUInt32(3));
                map.Add(entry.Name, entry);
            }
            while (result.NextRow());

            result.Dispose();

            return map;
        }

        string ReadSQLUpdate(string file)
        {
            try
            {
                return File.ReadAllText(file);
            }
            catch (Exception e)
            {
                FEL_LOG_FATAL("sql.updates", "Failed to open the sql update \"{0}\" for reading! Stopping the server to keep the database integrity, try to identify and solve the issue or disable the database updater.", file);
                throw new UpdateException($"Opening the sql update failed: {e.Message}!");
            }
        }

        uint Apply(string path)
        {
            // Benchmark query speed
            var now = Time.Now;

            // Update database
            _applyFile(path);

            return (uint)(Time.Now - now).TotalMilliseconds;
        }

        void UpdateEntry(ref AppliedFileEntry entry, uint speed = 0)
        {
            var update = "REPLACE INTO `updates` (`name`, `hash`, `state`, `speed`) VALUES (\"" +
                entry.Name + "\", \"" + entry.Hash + "\", \'" + entry.State.ToString() + "\', " + speed.ToString() + ")";

            // Update database
            _apply(update);
        }

        void RenameEntry(string from, string to)
        {
            // Delete the target if it exists
            {
                var update = "DELETE FROM `updates` WHERE `name`=\"" + to + "\"";

                // Update database
                _apply(update);
            }

            // Rename
            {
                var update = "UPDATE `updates` SET `name`=\"" + to + "\" WHERE `name`=\"" + from + "\"";

                // Update database
                _apply(update);
            }
        }

        void CleanUp(AppliedFileStorage storage)
        {
            if (storage.Count == 0)
                return;

            var update = new StringBuilder();
            var remaining = storage.Count;

            update.Append("DELETE FROM `updates` WHERE `name` IN(");

            foreach (var item in storage)
            {
                update.Append("\"");
                update.Append(item.Key);
                update.Append("\"");

                if ((--remaining) > 0)
                    update.Append(", ");
            }

            update.Append(")");

            // Update database
            _apply(update.ToString());
        }

        void UpdateState(string name, State state)
        {
            var update = "UPDATE `updates` SET `state`=\'" + state.ToString() + "\' WHERE `name`=\"" + name + "\"";

            // Update database
            _apply(update);
        }

        string _sourceDirectory;

        Action<string> _apply;
        Action<string> _applyFile;
        Func<string, QueryResult?> _retrieve;
    }
}
