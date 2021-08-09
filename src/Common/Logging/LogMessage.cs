// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Runtime.CompilerServices;

namespace Common
{
    public struct LogMessage
    {
        public LogMessage(LogLevel level, string type, string text)
        {
            Level = level;
            Type = type;
            Text = text;
            Prefix = string.Empty;
            Param1 = string.Empty;
            MTime = Time.Now;
        }
        public LogMessage(LogLevel level, string type, string text, string param1)
        {
            Level = level;
            Type = type;
            Text = text;
            Prefix = string.Empty;
            Param1 = param1;
            MTime = Time.Now;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string getTimeStr(DateTime time)
        {
            return time.ToLocalTime().ToString("yyyy-MM-dd_HH:mm:ss");
        }

        public string getTimeStr()
        {
            return getTimeStr(MTime);
        }

        public readonly LogLevel Level;
        public readonly string Type;
        public readonly string Text;
        public string Prefix;
        public string Param1;
        public DateTime MTime;

        ///@ Returns size of the log message content in bytes
        public int Size()
        {
            return Encoding.UTF8.GetByteCount(Prefix) + Encoding.UTF8.GetByteCount(Text);
        }
    }
}
