// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Diagnostics;
using static Common.Log;
using static Common.Errors;

namespace Common
{
    public static class ProcessPriority
    {
        public const string CONFIG_PROCESSOR_AFFINITY = "UseProcessors";
        public const string CONFIG_HIGH_PRIORITY = "ProcessPriority";

        public static void SetProcessPriority(string logChannel, int affinity, bool highPriority)
        {
            if (OperatingSystem.IsMacOS()) // macOS is not supported.
                return;

            Assert(affinity >= 0);

            var currentProcess = Process.GetCurrentProcess();

            int appAff = currentProcess.ProcessorAffinity.ToInt32();

            // remove non accessible processors
            var currentAffinity = affinity & appAff;

            if (currentAffinity == 0)
                FEL_LOG_ERROR(logChannel, "Processors marked in UseProcessors bitmask (hex) {0:X} are not accessible. Accessible processors bitmask (hex): {0:X}", affinity, appAff);
            else
            {
                try
                {
                    currentProcess.ProcessorAffinity = new IntPtr(currentAffinity);
                    FEL_LOG_INFO(logChannel, "Using processors (bitmask, hex): %x", currentAffinity);
                }
                catch
                {
                    FEL_LOG_ERROR(logChannel, "Can't set used processors (hex): %x", currentAffinity);
                }
            }

            if (highPriority)
            {
                try
                {
                    currentProcess.PriorityClass = ProcessPriorityClass.High;
                    FEL_LOG_INFO(logChannel, "Process priority class set to HIGH");
                }
                catch
                {
                    FEL_LOG_ERROR(logChannel, "Can't set process priority class.");
                }
            }
        }
    }
}
