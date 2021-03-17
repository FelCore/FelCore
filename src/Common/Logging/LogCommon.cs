// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

namespace Common
{
    public enum LogLevel : byte
    {
        LOG_LEVEL_DISABLED = 0,
        LOG_LEVEL_TRACE = 1,
        LOG_LEVEL_DEBUG = 2,
        LOG_LEVEL_INFO = 3,
        LOG_LEVEL_WARN = 4,
        LOG_LEVEL_ERROR = 5,
        LOG_LEVEL_FATAL = 6,

        NUM_ENABLED_LOG_LEVELS = LOG_LEVEL_FATAL, // SKIP
        LOG_LEVEL_INVALID = 0xFF // SKIP
    }

    public enum AppenderType : byte
    {
        APPENDER_NONE,
        APPENDER_CONSOLE,
        APPENDER_FILE,
        APPENDER_DB,

        APPENDER_INVALID = 0xFF // SKIP
    }

    public enum AppenderFlags
    {
        APPENDER_FLAGS_NONE = 0x00,
        APPENDER_FLAGS_PREFIX_TIMESTAMP = 0x01,
        APPENDER_FLAGS_PREFIX_LOGLEVEL = 0x02,
        APPENDER_FLAGS_PREFIX_LOGFILTERTYPE = 0x04,
        APPENDER_FLAGS_USE_TIMESTAMP = 0x08,
        APPENDER_FLAGS_MAKE_FILE_BACKUP = 0x10
    }
}

