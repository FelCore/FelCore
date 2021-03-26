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
using Server.Database;

namespace Server.AuthServer
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
                (s) =>
                {
                    FEL_LOG_INFO("server.authserver", "{0}", s);
                },
                () =>
                {
                    FEL_LOG_INFO("server.authserver", "Some extra info!");
                }
            );

            var pidFile = sConfigMgr.GetStringDefault("PidFile", "");
            if (!string.IsNullOrEmpty(pidFile))
            {
                var pid = Util.CreatePIDFile(pidFile);
                if (pid != 0)
                    FEL_LOG_INFO("server.authserver", "Daemon PID: {0}\n", pid);
                else
                {
                    FEL_LOG_ERROR("server.authserver", "Cannot create PID file {0}.\n", pidFile);
                    return 1;
                }
            }

            // Initialize the database connection
            if (!StartDB())
                return 1;

            System.Threading.Thread.Sleep(10);

            sLog.SetSynchronous();

            return 0;
        }

        /// Initialize connection to the database
        static bool StartDB()
        {
            // Load databases
            // NOTE: While authserver is singlethreaded you should keep synch_threads == 1.
            // Increasing it is just silly since only 1 will be used ever.
            var loader = new DatabaseLoader("server.authserver", (int)DatabaseTypeFlags.DATABASE_NONE);
            loader.AddDatabase(DB.LoginDatabase, "Login");

            if (!loader.Load())
                return false;

            FEL_LOG_INFO("server.authserver", "Started auth database connection pool.");
            sLog.SetRealmId(0); // Enables DB appenders when realm is set.
            return true;
        }

        /// Close the connection to the database
        static void StopDB()
        {
            DB.LoginDatabase.Close();
        }
    }
}
