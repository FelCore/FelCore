// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

namespace Common
{
    public static class Banner
    {
        public delegate void Log(string message);
        public delegate void LogExtraInfo();

        public static void Show(string appName, Log log, LogExtraInfo logExtraInfo)
        {
            log($"{GitRevision.FullVersion} ({appName})");
            log("<Ctrl-C> to stop.");

            log(@"
 ________ _______   ___       ________  ________  ________  _______      
|\  _____\\  ___ \ |\  \     |\   ____\|\   __  \|\   __  \|\  ___ \     
\ \  \__/\ \   __/|\ \  \    \ \  \___|\ \  \|\  \ \  \|\  \ \   __/|    
 \ \   __\\ \  \_|/_\ \  \    \ \  \    \ \  \\\  \ \   _  _\ \  \_|/__  
  \ \  \_| \ \  \_|\ \ \  \____\ \  \____\ \  \\\  \ \  \\  \\ \  \_|\ \ 
   \ \__\   \ \_______\ \_______\ \_______\ \_______\ \__\\ _\\ \_______\
    \|__|    \|_______|\|_______|\|_______|\|_______|\|__|\|__|\|_______|
                                                                         
            ");

            if (logExtraInfo != null)
                logExtraInfo();
        }
    }
}
