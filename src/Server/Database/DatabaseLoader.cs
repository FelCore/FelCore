// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using static Common.Log;
using static Common.ConfigMgr;
using static MySqlSharp.ErrorServer;
using Server.Database.Updater;

namespace Server.Database
{
    using Predicater = Func<bool>;
    using Closer = Action;

    public enum DatabaseTypeFlags : uint
    {
        DATABASE_NONE = 0,

        DATABASE_LOGIN = 1,
        DATABASE_CHARACTER = 2,
        DATABASE_WORLD = 4,

        DATABASE_MASK_ALL = DATABASE_LOGIN | DATABASE_CHARACTER | DATABASE_WORLD
    };

    public class DatabaseLoader
    {
        public static bool IsMySQL8 => MySqlSharp.NativeMethods.mysql_get_client_version() >= 80000;

        public DatabaseLoader(string logger, int defaultUpdateMask)
        {
            _logger = logger;
            _autoSetup = sConfigMgr.GetBoolDefault("Updates.AutoSetup", true);
            _updateFlags = (uint)sConfigMgr.GetIntDefault("Updates.EnableDatabases", defaultUpdateMask);

            _open = new Queue<Predicater>();
            _populate = new Queue<Predicater>();
            _update = new Queue<Predicater>();
            _prepare = new Queue<Predicater>();
            _close = new Stack<Closer>();
        }

        public void AddDatabase<T, Statements>(DatabaseWorkerPool<T, Statements> pool, string name)
            where T : MySqlConnection<Statements>
            where Statements : unmanaged, Enum
        {
            bool updatesEnabledForThis = DBUpdater<T, Statements>.IsEnabled(_updateFlags);

            _open.Enqueue(() =>
            {
                var dbString = sConfigMgr.GetStringDefault(name + "DatabaseInfo", "");
                if (dbString.Length == 0)
                {
                    FEL_LOG_ERROR(_logger, "Database {0} not specified in configuration file!", name);
                    return false;
                }

                var asyncThreads = (byte)sConfigMgr.GetIntDefault(name + "Database.WorkerThreads", 1);
                if (asyncThreads < 1 || asyncThreads > 32)
                {
                    FEL_LOG_ERROR(_logger, "{0} database: invalid number of worker threads specified. Please pick a value between 1 and 32.", name);
                    return false;
                }

                var synchThreads = (byte)sConfigMgr.GetIntDefault(name + "Database.SynchThreads", 1);

                pool.SetConnectionInfo(dbString, asyncThreads, synchThreads);

                var error = pool.Open();
                if (error != 0)
                {
                    // Database does not exist
                    if (error == (int)ER_BAD_DB_ERROR && updatesEnabledForThis && _autoSetup)
                    {
                        // Try to create the database and connect again if auto setup is enabled
                        if (DBUpdater<T, Statements>.Create(pool) && (pool.Open() == 0))
                            error = 0;
                    }

                    // If the error wasn't handled quit
                    if (error != 0)
                    {
                        FEL_LOG_ERROR("sql.driver", "Database {0} NOT opened. There were errors opening the MySQL connections. Check your SQLDriverLogFile ", name);
                        return false;
                    }
                }
                // Add the close operation
                _close.Push(() =>
                {
                    pool.Close();
                });
                return true;
            });

            if (updatesEnabledForThis)
            {
                _populate.Enqueue(() =>
                {
                    if (!DBUpdater<T, Statements>.Populate(pool))
                    {
                        FEL_LOG_ERROR(_logger, "Could not populate the {0} database, see log for details.", name);
                        return false;
                    }
                    return true;
                });

                _update.Enqueue(() =>
                {
                    if (!DBUpdater<T, Statements>.Update(pool))
                    {
                        FEL_LOG_ERROR(_logger, "Could not update the {0} database, see log for details.", name);
                        return false;
                    }
                    return true;
                });
            }

            _prepare.Enqueue(() =>
            {
                if (!pool.PrepareStatements())
                {
                    FEL_LOG_ERROR(_logger, "Could not prepare statements of the {0} database, see log for details.", name);
                    return false;
                }
                return true;
            });
        }

        public bool Load()
        {
            if (_updateFlags == 0)
                FEL_LOG_INFO("sql.updates", "Automatic database updates are disabled for all databases!");

            if (!OpenDatabases())
                return false;

            if (!PopulateDatabases())
                return false;

            if (!UpdateDatabases())
                return false;

            if (!PrepareStatements())
                return false;

            return true;
        }

        bool OpenDatabases()
        {
            return Process(_open);
        }

        bool PopulateDatabases()
        {
            return Process(_populate);
        }
        bool UpdateDatabases()
        {
            return Process(_update);
        }

        bool PrepareStatements()
        {
            return Process(_prepare);
        }

        // Invokes all functions in the given queue and closes the databases on errors.
        // Returns false when there was an error.
        bool Process(Queue<Predicater> queue)
        {
            while (queue.Count > 0)
            {
                if (!queue.Peek()())
                {
                    // Close all open databases which have a registered close operation
                    while (_close.Count > 0)
                    {
                        _close.Peek()();
                        _close.Pop();
                    }

                    return false;
                }

                queue.Dequeue();
            }
            return true;
        }

        string _logger;
        bool _autoSetup;
        uint _updateFlags;

        Queue<Predicater> _open, _populate, _update, _prepare;
        Stack<Closer> _close;
    }
}
