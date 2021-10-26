// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Cysharp.Text;

namespace Common
{
    public static class Errors
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert(bool condition, string? message = null)
        {
            Trace.Assert(condition, message);
            if (!condition)
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1>(bool condition, string message, Arg1 arg1)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2>(bool condition, string message, Arg1 arg1, Arg2 arg2)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2,Arg3>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2,Arg3,Arg4>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2,Arg3,Arg4,Arg5>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2,Arg3,Arg4,Arg5,Arg6>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Arg6 arg6)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5, arg6));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2,Arg3,Arg4,Arg5,Arg6,Arg7>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Arg6 arg6, Arg7 arg7)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert<Arg1,Arg2,Arg3,Arg4,Arg5,Arg6,Arg7,Arg8>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Arg6 arg6, Arg7 arg7, Arg8 arg8)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
                Environment.Exit(1); // Ensure app exit on assertion failure when debugger attacked.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert(bool condition, string? message = null)
        {
            if (!condition)
            {
                Trace.Assert(condition, message);
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1>(bool condition, string message, Arg1 arg1)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2>(bool condition, string message, Arg1 arg1, Arg2 arg2)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2,Arg3>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2,Arg3,Arg4>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2,Arg3,Arg4,Arg5>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2,Arg3,Arg4,Arg5,Arg6>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Arg6 arg6)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5, arg6));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2,Arg3,Arg4,Arg5,Arg6,Arg7>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Arg6 arg6, Arg7 arg7)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<Arg1,Arg2,Arg3,Arg4,Arg5,Arg6,Arg7,Arg8>(bool condition, string message, Arg1 arg1, Arg2 arg2, Arg3 arg3, Arg4 arg4, Arg5 arg5, Arg6 arg6, Arg7 arg7, Arg8 arg8)
        {
            if (!condition)
            {
                Trace.Assert(condition, ZString.Format(message, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
#if DEBUG
                Environment.Exit(1);; // Ensure app exit on assertion failure when debugger attacked.
#endif
            }
        }
    }
}
