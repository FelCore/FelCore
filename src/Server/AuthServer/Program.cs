using System;
using System.Collections.Generic;
using System.IO;
using Common;
using static Common.Log;
using static Common.ConfigMgr;
using static Common.Errors;

namespace AuthServer
{
    class Program
    {
        /// <summary>
        /// Fel Core Auth Server
        /// </summary>
        /// <param name="config" alias="c">Configuration file path</param>
        static int Main(string config = "authserver.conf")
        {
            Console.WriteLine($"The value for --config is: {config}");

            string configError = "";
            if (!sConfigMgr.LoadInitial(config,
                                        new List<string>(Environment.GetCommandLineArgs()),
                                        ref configError))
            {
                Console.WriteLine("Error in config file: {0}", configError);
                return 1;
            }

            //sLog.RegisterAppender<AppenderDB>();
            sLog.Initialize();

            //Assert(false);
            Banner.Show("AuthServer", "FelCore 0.1.0",
                (s) => {
                    FEL_LOG_INFO("server.authserver", "{0}", s);
                },
                () => {
                    FEL_LOG_INFO("server.authserver", "Some extra info!");
                }
            );

            return 0;
        }
    }
}
