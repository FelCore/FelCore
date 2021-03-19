// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using static Common.LogLevel;
using static Common.AppenderType;
using ColorTypes = System.ConsoleColor;

namespace Common
{
    public class AppenderConsole : Appender
    {
        public const AppenderType type = APPENDER_CONSOLE;
        public static int NUM_COLOR_TYPES = Enum.GetValues<ColorTypes>().Length;

        public AppenderConsole(byte id, string name, LogLevel level, AppenderFlags flags, string[] args)
            : base(id, name, level, flags)
        {
            for (byte i = 0; i < (int)NUM_ENABLED_LOG_LEVELS; ++i)
                _colors[i] = (ColorTypes)NUM_COLOR_TYPES;

            if (3 < args.Length)
                InitColors(name, args[3]);
        }

        public void InitColors(string name, string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                _colored = false;
                return;
            }

            var colorStrs = str.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (colorStrs.Length != (int)NUM_ENABLED_LOG_LEVELS)
            {
                throw new InvalidAppenderArgsException(string.Format("Log::CreateAppenderFromConfig: Invalid color data '{0}' for console appender {1} (expected {2} entries, got {3})",
                    str, name, NUM_ENABLED_LOG_LEVELS, colorStrs.Length));
            }

            for (byte i = 0; i < (int)NUM_ENABLED_LOG_LEVELS; ++i)
            {
                if (Enum.TryParse<ColorTypes>(colorStrs[i], true, out var result))
                {
                    _colors[i] = result;
                }
                else
                {
                    throw new InvalidAppenderArgsException(string.Format("Log::CreateAppenderFromConfig: Invalid color '{0}' for log level {1} on console appender {2}",
                        colorStrs[i], (LogLevel)i, name));
                }
            }

            _colored = true;
        }

        public override AppenderType Type => type;

        private void SetColor(ColorTypes color)
        {
            Console.ForegroundColor = color;
        }

        private void ResetColor()
        {
            Console.ResetColor();
        }

        protected override void _Write(ref LogMessage message)
        {
            bool stdout_stream = !(message.Level == LOG_LEVEL_ERROR || message.Level == LOG_LEVEL_FATAL);

            if (_colored)
            {
                byte index;
                switch (message.Level)
                {
                    case LOG_LEVEL_TRACE:
                        index = 5;
                        break;
                    case LOG_LEVEL_DEBUG:
                        index = 4;
                        break;
                    case LOG_LEVEL_INFO:
                        index = 3;
                        break;
                    case LOG_LEVEL_WARN:
                        index = 2;
                        break;
                    case LOG_LEVEL_FATAL:
                        index = 0;
                        break;
                    case LOG_LEVEL_ERROR:
                    default:
                        index = 1;
                        break;
                }

                SetColor(_colors[index]);
                if (stdout_stream)
                    Console.WriteLine("{0}{1}", message.Prefix, message.Text);
                else
                    Console.Error.WriteLine("{0}{1}", message.Prefix, message.Text);
                ResetColor();
            }
            else
            {
                if (stdout_stream)
                    Console.WriteLine("{0}{1}", message.Prefix, message.Text);
                else
                    Console.Error.WriteLine("{0}{1}", message.Prefix, message.Text);
            }
        }

        private bool _colored;
        private ColorTypes[] _colors = new ColorTypes[(int)NUM_ENABLED_LOG_LEVELS];
    }
}
