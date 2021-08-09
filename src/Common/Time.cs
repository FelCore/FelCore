// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Common
{
    public static class Time
    {
        public static readonly DateTime StartTime = DateTime.UtcNow;
        private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public static readonly DateTime UnixEpoch = DateTimeOffset.UnixEpoch.UtcDateTime;

        public static DateTime Now
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return StartTime + Stopwatch.Elapsed; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimestamp(DateTime time)
        {
            //NOTE time is assumed to be UTC time.
            return (long)time.Subtract(UnixEpoch).TotalSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetDateTime(long timestamp)
        {
            if (timestamp < 0) return DateTime.MinValue;
            return UnixEpoch.AddSeconds(timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetMSTime() => Stopwatch.ElapsedMilliseconds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetMSTimeDiff(long oldMSTime, long newMSTime)
        {
            // getMSTime() have limited data range and this is case when it overflow in this tick
            if (oldMSTime > newMSTime)
                return (0xFFFFFFFF - oldMSTime) + newMSTime;
            else
                return newMSTime - oldMSTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetMSTimeDiffToNow(long oldMSTime)
        {
            return GetMSTimeDiff(oldMSTime, GetMSTime());
        }
    }
}
