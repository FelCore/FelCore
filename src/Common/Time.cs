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
        public static uint GetTimestamp(DateTime time)
        {
            //NOTE time is assumed to be UTC time.
            return (uint)time.Subtract(UnixEpoch).TotalSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetDateTime(long timestamp)
        {
            if (timestamp < 0) return DateTime.MinValue;
            return UnixEpoch.AddSeconds(timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetMSTime() => (uint)Stopwatch.ElapsedMilliseconds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetMSTimeDiff(uint oldMSTime, uint newMSTime)
        {
            // getMSTime() have limited data range and this is case when it overflow in this tick
            if (oldMSTime > newMSTime)
                return (0xFFFFFFFF - oldMSTime) + newMSTime;
            else
                return newMSTime - oldMSTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetMSTimeDiffToNow(uint oldMSTime)
        {
            return GetMSTimeDiff(oldMSTime, GetMSTime());
        }
    }

    public class IntervalTimer
    {
        public void Update(long diff)
        {
            _current += diff;
            if (_current < 0)
                _current = 0;
        }

        public bool Passed()
        {
            return _current >= _interval;
        }

        public void Reset()
        {
            if (_current >= _interval)
                _current %= _interval;
        }

        public void SetCurrent(long current)
        {
            _current = current;
        }

        public void SetInterval(long interval)
        {
            _interval = interval;
        }

        public long GetInterval()
        {
            return _interval;
        }

        public long GetCurrent()
        {
            return _current;
        }

        long _interval;
        long _current;
    }
}
