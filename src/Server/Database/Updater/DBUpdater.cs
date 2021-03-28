// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.IO;
using System.Collections.Generic;
using Common;
using static Common.Log;
using static Common.Errors;
using static Common.ConfigMgr;

namespace Server.Database.Updater
{
    public class UpdateException : Exception
    {
        public UpdateException(string message)
        {
            Message = message;
        }

        public override string Message { get; }
    }

    public enum BaseLocation
    {
        LOCATION_REPOSITORY,
        LOCATION_DOWNLOAD
    }
    public static class DBUpdater<T, Statements>
        where T : MySqlConnectionProxy<Statements>
        where Statements : unmanaged, Enum
    {
        private static string _dbTypeName = typeof(T).Name;

        public static string GetConfigEntry()
        {
            switch (_dbTypeName)
            {
                case "LoginDatabaseConnection":
                    return "Updates.Auth";
                case "CharacterDatabaseConnection":
                    return "Updates.Character";
                case "WorldDatabaseConnection":
                    return "Updates.World";
            }

            return "";
        }

        public static string GetDatabaseName()
        {
            switch (_dbTypeName)
            {
                case "LoginDatabaseConnection":
                    return "Auth";
                case "CharacterDatabaseConnection":
                    return "Character";
                case "WorldDatabaseConnection":
                    return "World";
            }

            return "";
        }

        public static string GetBaseFile()
        {
            string baseDBFile = "";
            switch (_dbTypeName)
            {
                case "LoginDatabaseConnection":
                    baseDBFile = "/sql/base/fel_auth_database.sql";
                    break;
                case "CharacterDatabaseConnection":
                    baseDBFile = "/sql/base/fel_characters_database.sql";
                    break;
                case "WorldDatabaseConnection":
                    baseDBFile = DatabaseLoader.FULL_DATABASE;
                    break;
            }

            return sConfigMgr.GetStringDefault("SourceDirectory", ".") + baseDBFile;
        }

        public static bool IsEnabled(uint updateMask)
        {
            switch (_dbTypeName)
            {
                case "LoginDatabaseConnection":
                    return (updateMask & (uint)DatabaseTypeFlags.DATABASE_LOGIN) != 0;
                case "CharacterDatabaseConnection":
                    return (updateMask & (uint)DatabaseTypeFlags.DATABASE_CHARACTER) != 0;
                case "WorldDatabaseConnection":
                    return (updateMask & (uint)DatabaseTypeFlags.DATABASE_WORLD) != 0;
            }

            return false;
        }

        public static BaseLocation GetBaseLocationType()
        {
            switch (_dbTypeName)
            {
                case "WorldDatabaseConnection":
                    return BaseLocation.LOCATION_DOWNLOAD;
                default:
                    return BaseLocation.LOCATION_REPOSITORY;
            }
        }

        static bool CheckExecutable()
        {
            var exe = sConfigMgr.GetStringDefault("MySQLExecutable", "");

            return File.Exists(exe);
        }

        public static bool Create(DatabaseWorkerPool<T, Statements> pool)
        {
            var connectionInfo = pool.GetConnectionInfo();
            if (connectionInfo == null)
            {
                Assert(false, "Connection info was not set!");
                return false;
            }

            FEL_LOG_INFO("sql.updates", "Database \"{0}\" does not exist, do you want to create it? [yes (default) / no]: ", connectionInfo.Database);

            var answer = Console.ReadLine();

            if (!string.IsNullOrEmpty(answer) && !(answer.StartsWith('y') || answer.StartsWith('Y')))
                return false;

            FEL_LOG_INFO("sql.updates", "Creating database \"{0}\"...", connectionInfo.Database);

            // Path of temp file
            var temp = Path.GetTempFileName();

            // Create temporary query to use external MySQL CLi
            using (var file = File.OpenWrite(temp))
            using (var writer = new StreamWriter(file))
            {
                writer.Write("CREATE DATABASE `");
                writer.Write(connectionInfo.Database);
                writer.Write("` DEFAULT CHARACTER SET utf8 COLLATE utf8_general_ci");
                writer.WriteLine();
                writer.WriteLine();
            }

            try
            {
                DBUpdater<T, Statements>.ApplyFile(pool, connectionInfo.Host, connectionInfo.User, connectionInfo.Password,
                    connectionInfo.Port, "", temp);
            }
            catch (UpdateException)
            {
                FEL_LOG_FATAL("sql.updates", "Failed to create database {0}! Does the user (named in *.conf)"
                    + " have `CREATE`, `ALTER`, `DROP`, `INSERT` and `DELETE` privileges on the MySQL server?", connectionInfo.Database);
                File.Delete(temp);
                return false;
            }

            FEL_LOG_INFO("sql.updates", "Done.");
            File.Delete(temp);
            return true;
        }

        public static bool Update(DatabaseWorkerPool<T, Statements> pool)
        {
            if (!CheckExecutable())
                return false;

            FEL_LOG_INFO("sql.updates", "Updating {0} database...", GetDatabaseName());

            var sourceDirectory = sConfigMgr.GetStringDefault("SourceDirectory", "");

            if (!Directory.Exists(sourceDirectory))
            {
                FEL_LOG_ERROR("sql.updates", "DBUpdater: The given source directory {0} does not exist, "
                    + " change the path to the directory where your sql directory exists (for example c:\\source\\felycore). Shutting down.", sourceDirectory);
                return false;
            }

            UpdateFetcher updateFetcher = new UpdateFetcher(sourceDirectory,
                (query) => Apply(pool, query),
                (file) => ApplyFile(pool, file),
                (query) => Retrieve(pool, query));

            var result = new UpdateResult();
            try
            {
                result = updateFetcher.Update(
                    sConfigMgr.GetBoolDefault("Updates.Redundancy", true),
                    sConfigMgr.GetBoolDefault("Updates.AllowRehash", true),
                    sConfigMgr.GetBoolDefault("Updates.ArchivedRedundancy", false),
                    sConfigMgr.GetIntDefault("Updates.CleanDeadRefMaxCount", 3));
            }
            catch (UpdateException)
            {
                return false;
            }

            var info = string.Format("Containing {0} new and {1} archived updates.", result.Recent, result.Archived);

            if (result.Updated == 0)
                FEL_LOG_INFO("sql.updates", ">> {0} database is up-to-date! {1}", GetDatabaseName(), info);
            else
                FEL_LOG_INFO("sql.updates", ">> Applied {0} {1}. {2}", result.Updated, result.Updated == 1 ? "query" : "queries", info);

            return true;
        }

        public static bool Populate(DatabaseWorkerPool<T, Statements> pool)
        {
            {
                var result = Retrieve(pool, "SHOW TABLES");
                if (result != null && !result.IsEmpty())
                    return true;
            }

            if (!CheckExecutable())
                return false;

            FEL_LOG_INFO("sql.updates", "Database {0} is empty, auto populating it...", GetDatabaseName());

            var p = GetBaseFile();
            if (string.IsNullOrEmpty(p))
            {
                FEL_LOG_INFO("sql.updates", ">> No base file provided, skipped!");
                return true;
            }

            if (!File.Exists(p))
            {
                switch (GetBaseLocationType())
                {
                    case BaseLocation.LOCATION_REPOSITORY:
                    {
                        FEL_LOG_ERROR("sql.updates", ">> Base file \"{0}\" is missing. Try fixing it by cloning the source again.", p);

                        break;
                    }
                    case BaseLocation.LOCATION_DOWNLOAD:
                    {
                        var filename = Path.GetFileName(p);
                        var workdir = AppContext.BaseDirectory;

                        FEL_LOG_ERROR("sql.updates", ">> File \"{0}\" is missing, download it from \"https://github.com/FelCore/FelCore/releases\"" +
                            " uncompress it and place the file \"{1}\" in the directory \"{2}\".", filename, filename, workdir);
                        break;
                    }
                }
                return false;
            }

            // Update database
            FEL_LOG_INFO("sql.updates", ">> Applying \'{0}\'...", p);
            try
            {
                ApplyFile(pool, p);
            }
            catch (UpdateException)
            {
                return false;
            }

            FEL_LOG_INFO("sql.updates", ">> Done!");
            return true;
        }

        static QueryResult? Retrieve(DatabaseWorkerPool<T, Statements> pool, string query)
        {
            return pool.Query(query);
        }
        static void Apply(DatabaseWorkerPool<T, Statements> pool, string query)
        {
            pool.DirectExecute(query);
        }
        static void ApplyFile(DatabaseWorkerPool<T, Statements> pool, string path)
        {
            var connectionInfo = pool.GetConnectionInfo();

            if (connectionInfo == null)
            {
                Assert(false, "Connection info was not set!");
                return;
            }

            ApplyFile(pool, connectionInfo.Host, connectionInfo.User, connectionInfo.Password,
                connectionInfo.Port, connectionInfo.Database, path);
        }

        static void ApplyFile(DatabaseWorkerPool<T, Statements> pool, string host, string user, string password, string port_or_socket, string databaseName, string path)
        {
            List<string> args = new List<string>(7);

            // CLI Client connection info
            args.Add("-h" + host);
            args.Add("-u" + user);

            if (!string.IsNullOrEmpty(password))
                args.Add("-p" + password);

            // Check if we want to connect through ip or socket (Unix only)
            if (OperatingSystem.IsWindows())
            {
                if (host == ".")
                    args.Add("--protocol=PIPE");
                else
                    args.Add("-P" + port_or_socket);

            }
            else
            {
                if (!int.TryParse(port_or_socket[0].ToString(), out _))
                {
                    // We can't check if host == "." here, because it is named localhost if socket option is enabled
                    args.Add("-P0");
                    args.Add("--protocol=SOCKET");
                    args.Add("-S" + port_or_socket);
                }
                else
                    // generic case
                    args.Add("-P" + port_or_socket);
            }

            // Set the default charset to utf8
            args.Add("--default-character-set=utf8");

            // Set max allowed packet to 1 GB
            args.Add("--max-allowed-packet=1GB");

            // Database
            if (!string.IsNullOrEmpty(databaseName))
                args.Add(databaseName);

            // Invokes a mysql process which doesn't leak credentials to logs
            var ret = Util.StartProcess(sConfigMgr.GetStringDefault("MySQLExecutable", ""), string.Join(' ' , args), "sql.updates", path, true);

            if (ret != 0)
            {
                FEL_LOG_FATAL("sql.updates", "Applying of file \'{0}\' to database \'{1}\' failed!" +
                    " If you are a user, please pull the latest revision from the repository. " +
                    "Also make sure you have not applied any of the databases with your sql client. " +
                    "You cannot use auto-update system and import sql files from FelCore repository with your sql client. " +
                    "If you are a developer, please fix your sql query.",
                    path, databaseName);

                throw new UpdateException("update failed");
            }
        }
    }
}
