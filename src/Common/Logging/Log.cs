// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Collections.Generic;
using static Common.LogLevel;
using static Common.Errors;
using static Common.AppenderFlags;
using static Common.AppenderType;
using static Common.ConfigMgr;

namespace Common
{
    public class Log
    {
        static Log? _instance;

        public static Log sLog
        {
            get
            {
                if (_instance == null)
                    _instance = new Log();

                return _instance;
            }
        }

        public const string LOGGER_ROOT = "root";

        public delegate Appender AppenderCreatorFn(byte id, string name, LogLevel level, AppenderFlags flags, string[] extraArgs);

        public static Appender CreateAppender<T>(byte id, string name, LogLevel level, AppenderFlags flags, string[] extraArgs) where T : Appender
        {
            var obj = (Appender?)Activator.CreateInstance(typeof(T), new object[] { id, name, level, flags, extraArgs });
            if (obj == null)
                throw new ArgumentException(string.Format("CreateAppender failed for type {0}", typeof(T).Name));

            return obj;
        }

        private static string GetTimestampStr()
        {
            var now = Time.LocalNow;

            //       yyyy   year
            //       MM     month (2 digits 01-12)
            //       dd     day (2 digits 01-31)
            //       HH     hour (2 digits 00-23)
            //       mm     minutes (2 digits 00-59)
            //       ss     seconds (2 digits 00-59)
            return now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        Dictionary<byte, AppenderCreatorFn> _appenderFactory = new Dictionary<byte, AppenderCreatorFn>();
        Dictionary<byte, Appender> _appenders = new Dictionary<byte, Appender>();
        Dictionary<string, Logger> _loggers = new Dictionary<string, Logger>();

        byte _appenderId;
        LogLevel _lowestLogLevel;

        string? _logsDir;
        public string? LogsDir => _logsDir;

        string? _logsTimestamp;
        public string? LogsTimestamp => _logsTimestamp;

        LogWorker? _logWorker;
        ProducerConsumerQueue<LogOperation> _queue = new ProducerConsumerQueue<LogOperation>();

        private Log()
        {
            _appenderId = 0;
            _lowestLogLevel = LOG_LEVEL_FATAL;
            _logsTimestamp = "_" + GetTimestampStr();

            RegisterAppender<AppenderConsole>();
            RegisterAppender<AppenderFile>();
        }

        private byte NextAppenderId()
        {
            return _appenderId++;
        }
        public void RegisterAppender<T>() where T : Appender
        {
            var prop = typeof(T).GetProperty("Type");
            if (prop == null)
                throw new Exception(string.Format("The type {0} has no property named Type", typeof(T).Name));

            var val = prop.GetValue(null);
            if (val == null)
                throw new Exception();

            RegisterAppender((byte)val, CreateAppender<T>);
        }

        private void RegisterAppender(byte index, AppenderCreatorFn appenderCreateFn)
        {
            Assert(!_appenderFactory.ContainsKey(index));

            _appenderFactory[index] = appenderCreateFn;
        }
        private void CreateAppenderFromConfig(string? appenderName)
        {
            if (string.IsNullOrEmpty(appenderName))
                return;

            // Format = type, level, flags, optional1, optional2
            // if type = File. optional1 = file and option2 = mode
            // if type = Console. optional1 = Color
            var options = sConfigMgr.GetStringDefault(appenderName, "");

            var tokens = options.Split(',', StringSplitOptions.TrimEntries);

            var size = tokens.Length;
            var name = appenderName.Substring(9);

            if (size < 2)
            {
                Console.Error.WriteLine(string.Format("Log::CreateAppenderFromConfig: Wrong configuration for appender {0}. Config line: {1}", name, options));
                return;
            }

            AppenderFlags flags = APPENDER_FLAGS_NONE;
            AppenderType type = APPENDER_INVALID;
            Enum.TryParse<AppenderType>(tokens[0], out type);

            LogLevel level = LOG_LEVEL_INVALID;
            Enum.TryParse<LogLevel>(tokens[1], out level);

            if (!_appenderFactory.TryGetValue((byte)type, out var factoryFunction))
            {
                Console.Error.WriteLine(string.Format("Log::CreateAppenderFromConfig: Unknown type '{0}' for appender {1}", tokens[0], name));
                return;
            }

            if (level > NUM_ENABLED_LOG_LEVELS)
            {
                Console.Error.WriteLine(string.Format("Log::CreateAppenderFromConfig: Wrong Log Level '{0}' for appender {1}", tokens[1], name));
                return;
            }

            if (size > 2)
            {
                if (byte.TryParse(tokens[2], out var flagsVal))
                    flags = (AppenderFlags)flagsVal;
                else
                {
                    Console.Error.WriteLine(string.Format("Log::CreateAppenderFromConfig: Unknown flags '{0}' for appender {1}", tokens[2], name));
                    return;
                }
            }

            try
            {
                var appender = factoryFunction(NextAppenderId(), name, level, flags, tokens);
                _appenders[appender.Id] = appender;
            }
            catch (InvalidAppenderArgsException ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private void CreateLoggerFromConfig(string appenderName)
        {
            if (string.IsNullOrEmpty(appenderName))
                return;

            LogLevel level = LOG_LEVEL_DISABLED;

            var options = sConfigMgr.GetStringDefault(appenderName, "");
            var name = appenderName.Substring(7);

            if (string.IsNullOrEmpty(options))
            {
                Console.Error.WriteLine(string.Format("Log::CreateLoggerFromConfig: Missing config option Logger.{0}", name));
                return;
            }

            var tokens = options.Split(',', StringSplitOptions.TrimEntries);

            if (tokens.Length != 2)
            {
                Console.Error.WriteLine(string.Format("Log::CreateLoggerFromConfig: Wrong config option Logger.{0}={1}", name, options));
                return;
            }

            Logger? logger = null;
            _loggers.TryGetValue(name, out logger);
            if (logger != null)
            {
                Console.Error.WriteLine(string.Format("Error while configuring Logger {0}. Already defined", name));
                return;
            }

            level = LOG_LEVEL_INVALID;
            Enum.TryParse<LogLevel>(tokens[0], out level);

            if (level > NUM_ENABLED_LOG_LEVELS)
            {
                Console.Error.WriteLine(string.Format("Log::CreateLoggerFromConfig: Wrong Log Level '{0}' for logger {1}", tokens[0], name));
                return;
            }

            if (level < _lowestLogLevel)
                _lowestLogLevel = level;

            _loggers[name] = logger = new Logger(name, level);
            //Console.WriteLine(string.Format("Log::CreateLoggerFromConfig: Created Logger {0}, Level {1}", name, level));

            tokens = tokens[1].Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in tokens)
            {
                var appender = GetAppenderByName(item);
                if (appender != null)
                {
                    logger.AddAppender(appender.Id, appender);
                    //Console.WriteLine(string.Format("Log::CreateLoggerFromConfig: Added Appender {0} to Logger {1}", appender.Name, name));
                }
                else
                    Console.Error.WriteLine(string.Format("Error while configuring Appender {0} in Logger {1}. Appender does not exist", item, name));
            }
        }

        private void ReadAppendersFromConfig()
        {
            List<string> keys = sConfigMgr.GetKeysByString("Appender.");

            foreach (var appenderName in keys)
                CreateAppenderFromConfig(appenderName);
        }

        private void ReadLoggersFromConfig()
        {
            var keys = sConfigMgr.GetKeysByString("Logger.");
            foreach (var loggerName in keys)
                CreateLoggerFromConfig(loggerName);

            // Bad config configuration, creating default config
            if (!_loggers.ContainsKey(LOGGER_ROOT))
            {
                Console.Error.WriteLine("Wrong Loggers configuration. Review your Logger config section.");
                Console.Error.WriteLine("Creating default loggers [root (Error), server (Info)] to console");

                Close();

                var appender = new AppenderConsole(NextAppenderId(), "Console", LOG_LEVEL_DEBUG, APPENDER_FLAGS_NONE, new string[0]);
                _appenders[appender.Id] = appender;

                var rootLogger = new Logger(LOGGER_ROOT, LOG_LEVEL_ERROR);
                rootLogger.AddAppender(appender.Id, appender);
                _loggers[LOGGER_ROOT] = rootLogger;

                var serverLogger = new Logger("server", LOG_LEVEL_INFO);
                serverLogger.AddAppender(appender.Id, appender);
                _loggers["server"] = serverLogger;
            }
        }

        public void SetRealmId(uint id)
        {
            foreach (var appender in _appenders)
                appender.Value.SetRealmId(id);
        }

        public void Initialize(bool asyncEnabled = false)
        {
            if (asyncEnabled)
                _logWorker = new LogWorker(_queue, 1);

            LoadFromConfig();
        }

        public void SetSynchronous() // Not threadsafe - should only be called from main() after all threads are joined
        {
            if (_logWorker == null) return;

            _logWorker.Dispose();
            _logWorker = null;
        }

        public void LoadFromConfig()
        {
            Close();

            _lowestLogLevel = LOG_LEVEL_FATAL;
            _appenderId = 0;
            _logsDir = sConfigMgr.GetStringDefault("LogsDir", "");

            ReadAppendersFromConfig();
            ReadLoggersFromConfig();
        }
        public void Close()
        {
            _loggers.Clear();
            _appenders.Clear();
        }

        private Logger? GetLoggerByType(string type)
        {
            if (_loggers.TryGetValue(type, out var ret))
                return ret;

            if (type == LOGGER_ROOT)
                return null;

            var parentLogger = LOGGER_ROOT;
            var found = type.LastIndexOf('.');
            if (found != -1)
                parentLogger = type.Substring(0, found);

            return GetLoggerByType(parentLogger);
        }

        public bool ShouldLog(string type, LogLevel level)
        {
            // TODO: Use cache to store "Type.sub1.sub2": "Type" equivalence, should
            // Speed up in cases where requesting "Type.sub1.sub2" but only configured
            // Logger "Type"

            // Don't even look for a logger if the LogLevel is lower than lowest log levels across all loggers
            if (level < _lowestLogLevel)
                return false;

            var logger = GetLoggerByType(type);
            if (logger == null)
                return false;

            LogLevel logLevel = logger.LogLevel;
            return logLevel != LOG_LEVEL_DISABLED && logLevel <= level;
        }

        private Appender? GetAppenderByName(string name)
        {
            foreach(var pair in _appenders)
            {
                if (pair.Value != null && pair.Value.Name == name)
                    return pair.Value;
            }

            return null;
        }

        public bool SetLogLevel(string name, int newLeveli, bool isLogger = true)
        {
            if (newLeveli < 0)
                return false;

            LogLevel newLevel = (LogLevel)newLeveli;

            if (isLogger)
            {
                Logger? search = null;
                foreach(var pair in _loggers)
                {
                    if (pair.Value.Name == name)
                    {
                        search = pair.Value;
                        break;
                    }
                }

                if (search == null) return false;

                search.LogLevel = newLevel;

                if (newLevel != LOG_LEVEL_DISABLED && newLevel < _lowestLogLevel)
                    _lowestLogLevel = newLevel;
            }
            else
            {
                Appender? appender = GetAppenderByName(name);
                if (appender == null)
                    return false;

                appender.LogLevel = newLevel;
            }

            return true;
        }

        private void Write(ref LogMessage msg)
        {
            var logger = GetLoggerByType(msg.Type);
            if (logger == null) return;

            if (_logWorker != null)
            {
                var logOperation = new LogOperation(logger, ref msg);
                _queue.Push(logOperation);
            }
            else
                logger.Write(ref msg);
        }

        public void outMessage(string filter, LogLevel level, string message)
        {
            var msg = new LogMessage(level, filter, message);;
            Write(ref msg);
        }

        public void outMessage(string filter, LogLevel level, string format, params object?[] args)
        {
            var msg = new LogMessage(level, filter, string.Format(format, args));;
            Write(ref msg);
        }

        public void outCommand(string message, string param1)
        {
            var msg = new LogMessage(LOG_LEVEL_INFO, "commands.gm", message, param1);
            Write(ref msg);
        }

        public void outCommand(uint account, string format, params object?[] args)
        {
            outCommand(string.Format(format, args), account.ToString());
        }

        public void outCharDump(string? str, uint accountId, ulong guid, string name)
        {
            if (string.IsNullOrEmpty(str) || !ShouldLog("entities.player.dump", LOG_LEVEL_INFO))
                return;

            StringBuilder sb = new StringBuilder();
            sb.Append("== START DUMP == (account: ");
            sb.Append(accountId);
            sb.Append(" guid: ");
            sb.Append(guid);
            sb.Append(" name: ");
            sb.Append(name);
            sb.Append(")");
            sb.AppendLine();
            sb.Append(str);
            sb.AppendLine();
            sb.Append("== END DUMP ==");
            sb.AppendLine();

            var msg = new LogMessage(LOG_LEVEL_INFO, "entities.player.dump", sb.ToString());

            sb.Clear();
            sb.Append(guid);
            sb.Append('_');
            sb.Append(name);
            msg.Param1 = sb.ToString();

            Write(ref msg);
        }

        public static void FEL_LOG_TRACE(string filter, string format, params object?[] args)
        {
#if !PERFORMANCE_PROFILING
            if (sLog.ShouldLog(filter, LOG_LEVEL_TRACE))
                sLog.outMessage(filter, LOG_LEVEL_TRACE, format, args);
#endif
        }

        public static void FEL_LOG_DEBUG(string filter, string format, params object?[] args)
        {
#if !PERFORMANCE_PROFILING
            if (sLog.ShouldLog(filter, LOG_LEVEL_DEBUG))
                sLog.outMessage(filter, LOG_LEVEL_DEBUG, format, args);
#endif
        }

        public static void FEL_LOG_INFO(string filter, string format, params object?[] args)
        {
#if !PERFORMANCE_PROFILING
            if (sLog.ShouldLog(filter, LOG_LEVEL_INFO))
                sLog.outMessage(filter, LOG_LEVEL_INFO, format, args);
#endif
        }

        public static void FEL_LOG_WARN(string filter, string format, params object?[] args)
        {
#if !PERFORMANCE_PROFILING
            if (sLog.ShouldLog(filter, LOG_LEVEL_WARN))
                sLog.outMessage(filter, LOG_LEVEL_WARN, format, args);
#endif
        }

        public static void FEL_LOG_ERROR(string filter, string format, params object?[] args)
        {
#if !PERFORMANCE_PROFILING
            if (sLog.ShouldLog(filter, LOG_LEVEL_ERROR))
                sLog.outMessage(filter, LOG_LEVEL_ERROR, format, args);
#endif
        }

        public static void FEL_LOG_FATAL(string filter, string format, params object?[] args)
        {
#if !PERFORMANCE_PROFILING
            if (sLog.ShouldLog(filter, LOG_LEVEL_FATAL))
                sLog.outMessage(filter, LOG_LEVEL_FATAL, format, args);
#endif
        }
    }
}
