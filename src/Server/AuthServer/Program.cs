// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using Common;
using static Common.Log;
using static Common.ConfigMgr;
using static Common.Errors;
using static Common.ProcessPriority;
using Server.Database;
using static Server.Database.LoginStatements;
using static Server.AuthServer.AuthSocketMgr;
using static Server.Shared.RealmList;

namespace Server.AuthServer
{
    class Program
    {
        static bool Stop;

        static void CreateAccount(string username, string password, string email)
        {
            username = username.ToUpperInvariant();
            password = password.ToUpperInvariant();

            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_INS_ACCOUNT);

            stmt.Parameters[0] = username;
            var data = SRP6.MakeRegistrationData(username, password);
            stmt.Parameters[1] = data.Item1; // salt
            stmt.Parameters[2] = data.Item2; // verifier
            stmt.Parameters[3] = email;
            stmt.Parameters[4] = email;

            DB.LoginDatabase.DirectExecute(stmt);

            stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_INS_REALM_CHARACTERS_INIT);
            DB.LoginDatabase.Execute(stmt);
        }

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

            sLog.RegisterAppender<AppenderDB>();
            sLog.Initialize();

            Banner.Show("AuthServer",
                (s) =>
                {
                    FEL_LOG_INFO("server.authserver", "{0}", s);
                },
                () =>
                {
                    FEL_LOG_INFO("server.authserver", "Using configuration file {0}.", sConfigMgr.Filename);
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

            // Get the list of realms for the server
            sRealmList.Initialize(sConfigMgr.GetIntDefault("RealmsStateUpdateDelay", 20));

            if (sRealmList.GetRealms().Count == 0)
            {
                FEL_LOG_ERROR("server.authserver", "No valid realms specified.");
                return 1;
            }

            // Start the listening port (acceptor) for auth connections
            var port = sConfigMgr.GetIntDefault("RealmServerPort", 3724);
            if (port < 0 || port > 0xFFFF)
            {
                FEL_LOG_ERROR("server.authserver", "Specified port out of allowed range (1-65535)");
                return 1;
            }

            var bindIp = sConfigMgr.GetStringDefault("BindIP", "0.0.0.0");

            if (!sAuthSocketMgr.StartNetwork(bindIp, port))
            {
                FEL_LOG_ERROR("server.authserver", "Failed to initialize network");
                return 1;
            }

            // Set process priority according to configuration settings
            SetProcessPriority("server.authserver", sConfigMgr.GetIntDefault(CONFIG_PROCESSOR_AFFINITY, 0), sConfigMgr.GetBoolDefault(CONFIG_HIGH_PRIORITY, false));

            Console.CancelKeyPress += delegate(object? sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                Stop = true;
            };

            var dbPingInterval = sConfigMgr.GetIntDefault("MaxPingTime", 30) * 1000;
            Timer? mysqlKeepAliveTimer = null;
            mysqlKeepAliveTimer = new Timer((s) =>
            {
                FEL_LOG_INFO("server.authserver", "Ping MySQL to keep connection alive");
                DB.LoginDatabase.KeepAlive();

                mysqlKeepAliveTimer!.Change(dbPingInterval, Timeout.Infinite);
            }, null, dbPingInterval, Timeout.Infinite);

            var banExpiryCheckInterval = sConfigMgr.GetIntDefault("BanExpiryCheckInterval", 60) * 1000;
            Timer? banExpiryCheckTimer = null;
            banExpiryCheckTimer = new Timer((s) =>
            {
                DB.LoginDatabase.Execute(DB.LoginDatabase.GetPreparedStatement(LOGIN_DEL_EXPIRED_IP_BANS));
                DB.LoginDatabase.Execute(DB.LoginDatabase.GetPreparedStatement(LOGIN_UPD_EXPIRED_ACCOUNT_BANS));

                banExpiryCheckTimer!.Change(banExpiryCheckInterval, Timeout.Infinite);
            }, null, banExpiryCheckInterval, Timeout.Infinite);

            while (!Stop)
            {
                Thread.Sleep(100);
            }

            sRealmList.Close();
            mysqlKeepAliveTimer.Dispose();
            banExpiryCheckTimer.Dispose();

            FEL_LOG_INFO("server.authserver", "Halting process...");

            StopDB();
            sAuthSocketMgr.StopNetwork();

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
