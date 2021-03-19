// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System.Collections.Generic;

namespace Common
{
    public class Logger
    {
        public Logger(string name, LogLevel level)
        {
            _name = name;
            _level = level;
            _appenders = new Dictionary<byte, Appender>();
        }

        public void AddAppender(byte id, Appender appender)
        {
            _appenders[id] = appender;
        }

        public void DelAppender(byte id)
        {
            _appenders.Remove(id);
        }

        public string Name => _name;
        public LogLevel LogLevel
        {
            get { return _level; }
            set { _level = value; }
        }

        public void Write(ref LogMessage message)
        {
            if (_level == 0 || _level > message.Level || string.IsNullOrEmpty(message.Text))
                return;

            foreach (var pair in _appenders)
                if (pair.Value != null)
                    pair.Value.Write(ref message);
        }

        string _name;
        LogLevel _level;
        Dictionary<byte, Appender> _appenders;
    }
}
