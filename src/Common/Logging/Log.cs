// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using static Common.LogLevel;
using static Common.AppenderType;
using ColorTypes = System.ConsoleColor;

namespace Common
{
    public class Log
    {
        static Log? _instance;

        public static Log Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Log();

                return _instance;
            }
        }

        string? m_logsDir;
        public string? LogsDir => m_logsDir;

        string? m_logsTimestamp;
        public string? LogsTimestamp => m_logsTimestamp;

        private Log()
        {

        }
    }
}
