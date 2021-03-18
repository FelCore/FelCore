// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using static Common.LogLevel;
using static Common.AppenderFlags;

namespace Common
{
    public class Appender : IDisposable
    {
        private bool _disposed;
        public bool Disposed
        {
            get { return _disposed; }
            protected set { _disposed = value; }
        }

        public Appender(byte id, string name, LogLevel level = LOG_LEVEL_DISABLED, AppenderFlags flags = APPENDER_FLAGS_NONE)
        {
            _id = id;
            _name = name;
            _level = level;
            _flags = flags;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }

        public byte getId()
        {
            return _id;
        }

        public string getName()
        {
            return _name;
        }

        public virtual AppenderType getType() { return AppenderType.APPENDER_NONE; }

        public LogLevel getLogLevel()
        {
            return _level;
        }

        public AppenderFlags getFlags()
        {
            return _flags;
        }

        public void setLogLevel(LogLevel level)
        {
            _level = level;
        }

        public void write(ref LogMessage message)
        {
            if (_level == LOG_LEVEL_DISABLED || _level > message.Level)
                return;

            var sb = new StringBuilder();

            if ((_flags & APPENDER_FLAGS_PREFIX_TIMESTAMP) != 0)
            {
                sb.Append(message.getTimeStr());
                sb.Append(" ");
            }

            if ((_flags & APPENDER_FLAGS_PREFIX_LOGLEVEL) != 0 )
                sb.AppendFormat("{0,-5} ", getLogLevelString(message.Level));

            if ((_flags & APPENDER_FLAGS_PREFIX_LOGFILTERTYPE) != 0)
            {
                sb.Append("[");
                sb.Append(message.Type);
                sb.Append("] ");
            }

            message.Prefix = sb.ToString();

            _write(ref message);
        }

        public static string getLogLevelString(LogLevel level)
        {
            switch (level)
            {
                case LOG_LEVEL_FATAL:
                    return "FATAL";
                case LOG_LEVEL_ERROR:
                    return "ERROR";
                case LOG_LEVEL_WARN:
                    return "WARN";
                case LOG_LEVEL_INFO:
                    return "INFO";
                case LOG_LEVEL_DEBUG:
                    return "DEBUG";
                case LOG_LEVEL_TRACE:
                    return "TRACE";
                default:
                    return "DISABLED";
            }
        }

        public virtual void setRealmId(uint realmId) { }

        protected virtual void _write(ref LogMessage message) { }

        private byte _id;
        private string _name;
        private LogLevel _level;
        private AppenderFlags _flags;
    }

    public class InvalidAppenderArgsException : ArgumentException
    {
        public InvalidAppenderArgsException(string? message) : base(message) { }
    }
}
