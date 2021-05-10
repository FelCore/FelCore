// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Common
{
    public static partial class GitRevision
    {
        public const string Hash = ThisAssembly.Git.Commit;
        public const string Date = ThisAssembly.Git.CommitDate;
        public const string Branch = ThisAssembly.Git.Branch;
        public const string Tag = ThisAssembly.Git.Tag;
        public const string Major = ThisAssembly.Git.SemVer.Major;
        public const string Minor = ThisAssembly.Git.SemVer.Minor;
        public const string Patch = ThisAssembly.Git.SemVer.Patch;
        public static readonly string FileVersion = $"{Hash}({Major}.{Minor}.{Patch}) {Date} ({Branch})";
        public static readonly string DotnetRuntime;
        public static readonly string OSPlatform;
        public static readonly string FullVersion;
        public static readonly string HostOSVersion = $"{Environment.OSVersion.Platform} {Environment.OSVersion.VersionString}";
        public const string FullDatabase = "FELDB_full_world.sql";

        static GitRevision()
        {
            DotnetRuntime = string.Format("Runtime: {0} (Mode: {1}) ",
                RuntimeInformation.FrameworkDescription,
                Environment.Version.Major >= 6 && !RuntimeFeature.IsDynamicCodeSupported ? "NativeAOT" : "JIT"
            );

            if (OperatingSystem.IsWindows())
            {
                if (Environment.Is64BitOperatingSystem)
                    OSPlatform = "Win64";
                else
                    OSPlatform = "Win32";
            }
            else if (OperatingSystem.IsMacOS())
                OSPlatform = "macOS";
            else
                OSPlatform = "Unix";

            FullVersion = $"FelCore rev. {Hash}({Major}.{Minor}.{Patch}) {Date} ({Branch}) ({OSPlatform})";
        }
    }
}
