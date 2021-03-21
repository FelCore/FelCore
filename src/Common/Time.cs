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
        private static readonly DateTime StartTime = DateTime.UtcNow;
        private static readonly DateTime LocalStartTime = DateTime.Now;
        private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public static DateTime Now
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return StartTime + Stopwatch.Elapsed; }
        }

        public static DateTime LocalNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return LocalStartTime + Stopwatch.Elapsed; }
        }
    }
}
