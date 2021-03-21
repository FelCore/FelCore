// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using Common;
using static Common.Log;
using static Common.ConfigMgr;
using static Common.Errors;

namespace AuthServer
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<string>(
                    new string[] { "--config", "-c" },
                    getDefaultValue: () => "authserver.conf",
                    description: "Configuration file path")
            };
            rootCommand.Description = "Fel Core Auth Server";

            string configPath = "";

            rootCommand.Handler = CommandHandler.Create<string>((config) =>
            {
                configPath = config;
            });

            rootCommand.Invoke(args);

            if (string.IsNullOrEmpty(configPath)) // Handle --help or --version option here.
                return 0;

            string configError = "";
            if (!sConfigMgr.LoadInitial(configPath,
                                        new List<string>(Environment.GetCommandLineArgs()),
                                        ref configError))
            {
                Console.WriteLine("Error in config file: {0}", configError);
                return 1;
            }

            //sLog.RegisterAppender<AppenderDB>();
            sLog.Initialize(true);

            //Assert(false);
            Banner.Show("AuthServer", "FelCore 0.1.0",
                (s) => {
                    FEL_LOG_INFO("server.authserver", "{0}", s);
                },
                () => {
                    FEL_LOG_INFO("server.authserver", "Some extra info!");
                }
            );

            System.Threading.Thread.Sleep(1000);

            sLog.SetSynchronous();

            return 0;
        }
    }
}
