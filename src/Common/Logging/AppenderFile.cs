// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.IO;
using System.Threading;
using static Common.LogLevel;
using static Common.AppenderType;
using static Common.AppenderFlags;

namespace Common
{
    public class AppenderFile : Appender
    {
        //public:
        public static AppenderType type = APPENDER_FILE;

        public AppenderFile(byte id, string name, LogLevel level, AppenderFlags flags, string[] args) : base(id, name, level, flags)
        {
            _logDir = Log.Instance.LogsDir;
            if (args.Length < 4)
                throw new InvalidAppenderArgsException(string.Format("Log::CreateAppenderFromConfig: Missing file name for appender {0}", name));

            _fileName = args[3];

            string mode = "a";
            if (4 < args.Length)
                mode = args[4];

            if ((flags & APPENDER_FLAGS_USE_TIMESTAMP) != 0)
            {
                var dot_pos = _fileName.LastIndexOf('.');
                if (dot_pos != -1)
                    _fileName.Insert(dot_pos, Log.Instance.LogsTimestamp ?? string.Empty);
                else
                    _fileName += Log.Instance.LogsTimestamp;
            }

            if (5 < args.Length)
            {
                //if (Optional<uint32> size = Trinity::StringTo<uint32>(args[5]))
                if (long.TryParse(args[5], out var result))
                    _maxFileSize = result;
                else
                    throw new InvalidAppenderArgsException(string.Format("Log::CreateAppenderFromConfig: Invalid size '{0}' for appender {1}", args[5], name));
            }

            _dynamicName = _fileName.IndexOf("{0}") != -1;
            _backup = (flags & APPENDER_FLAGS_MAKE_FILE_BACKUP) != 0;

            if (!_dynamicName)
                _logfile = OpenFile(_fileName, mode, (mode == "w") && _backup);
        }

        protected override void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
                CloseFile();
                Disposed = true;
            }
        }
        public FileStream? OpenFile(string filename, string mode, bool backup)
        {
            string fullName = Path.Combine(_logDir ?? ".", filename);
            if (backup)
            {
                CloseFile();

                string newName = new string(fullName);
                newName += '.';
                newName += LogMessage.getTimeStr(Time.Now);
                newName = newName.Replace(':', '-');

                try
                {
                    File.Move(fullName, newName); // no error handling... if we couldn't make a backup, just ignore
                }
                catch {}
            }

            FileMode fileMode = FileMode.Open;
            FileAccess fileAccess = FileAccess.Read;
            switch(mode)
            {
                case "r":
                    fileMode = FileMode.Open;
                    fileAccess = FileAccess.Read;
                    break;
                case "w":
                    fileMode = FileMode.Open;
                    fileAccess = FileAccess.Write;
                    break;
                case "a":
                    fileMode = FileMode.Append;
                    fileAccess = FileAccess.Write;
                    break;
                case "r+":
                    fileMode = FileMode.Open;
                    fileAccess = FileAccess.ReadWrite;
                    break;
                case "w+":
                    fileMode = FileMode.Open;
                    fileAccess = FileAccess.ReadWrite;
                    break;
                case "a+":
                    fileMode = FileMode.Append;
                    fileAccess = FileAccess.ReadWrite;
                    break;
            }

            try
            {
                var file = File.Open(fullName, fileMode, fileAccess);
                Interlocked.Exchange(ref _fileSize, file.Length);
                return file;
            }
            catch {}

            return null;
        }
        public override AppenderType getType() { return type; }

        void CloseFile()
        {
            if (_logfile == null) return;

            _logfile.Dispose();
            _logfile = null;
        }

        protected override void _write(ref LogMessage message)
        {
            bool exceedMaxSize = _maxFileSize > 0 && (_fileSize + message.Size()) > _maxFileSize;

            if (_dynamicName)
            {
                if (_fileName != null)
                {
                    var newFileName = string.Format(_fileName, message.Param1);

                    try
                    {
                        var file = OpenFile(newFileName, "a", _backup || exceedMaxSize);
                        if (file == null) return;

                        using (var writer = new StreamWriter(file))
                        {
                            writer.Write(string.Format("{0}{0}", message.Prefix, message.Text));
                            writer.Write(Environment.NewLine);
                        }
                        Interlocked.Add(ref _fileSize, message.Size());

                        file.Dispose();
                    }
                    catch {}

                    return;
                }
            }
            else if (exceedMaxSize)
            {
                if (_fileName != null)
                    _logfile = OpenFile(_fileName, "w", true);
            }

            if (_logfile == null)
                return;

            using (var writer = new StreamWriter(_logfile))
            {
                writer.Write(string.Format("{0}{0}", message.Prefix, message.Text));
                writer.Write(Environment.NewLine);
            }

            Interlocked.Add(ref _fileSize, message.Size());
        }

        FileStream? _logfile;
        string? _fileName;
        string? _logDir;
        bool _dynamicName;
        bool _backup;
        long _maxFileSize;
        long _fileSize;
    }
}
