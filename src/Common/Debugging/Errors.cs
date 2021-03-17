// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Diagnostics;

namespace Common
{
    public static class Errors
    {
        public static void Assert(bool condition, string? message = null)
        {
            Trace.Assert(condition, message);
            if (!condition)
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
        }

        public static void DebugAssert(bool condition, string? message = null)
        {
            Trace.Assert(condition, message);
#if DEBUG
            if (!condition)
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
#endif
        }
    }
}
