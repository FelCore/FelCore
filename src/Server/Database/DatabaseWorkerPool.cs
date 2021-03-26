// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;
using System.Collections.Generic;
using MySqlConnector;
using Common;
using static Common.Log;
using static Common.Errors;

namespace Server.Database
{
    using static InternalIndex;

    enum InternalIndex
    {
        IDX_ASYNC,
        IDX_SYNCH,
        IDX_SIZE
    };

    public class PingOperation : SqlOperation
    {
        //! Operation for idle delaythreads
        public override bool Execute()
        {
            if (Conn == null)
                return false;

            Conn.Ping();
            return true;
        }
    }

    public class DatabaseWorkerPool<T, Statements> : IDisposable
        where T : MySqlConnectionProxy<Statements>
        where Statements : unmanaged, Enum
    {
        ProducerConsumerQueue<SqlOperation> _queue;

        List<T>[] _connections = new List<T>[(int)IDX_SIZE];

        MySqlConnectionInfo? _connectionInfo;

        byte _async_threads, _synch_threads;

        static ThreadLocal<bool> _warnSyncQueries = new();

        bool _disposed;

        public DatabaseWorkerPool()
        {
            _connections[0] = new List<T>();
            _connections[1] = new List<T>();

            _queue = new ProducerConsumerQueue<SqlOperation>();
        }

        public void SetConnectionInfo(string infoString, byte asyncThreads, byte synchThreads)
        {
            _connectionInfo = new MySqlConnectionInfo(infoString);

            _async_threads = asyncThreads;
            _synch_threads = synchThreads;
        }

        public string GetDatabaseName()
        {
            return _connectionInfo == null ? string.Empty : _connectionInfo.Database;
        }

        /// <summary>
        /// Delayed one-way statement methods.
        /// <para>Enqueues a one-way SQL operation in string format that will be executed asynchronously.</para>
        /// <para>This method should only be used for queries that are only executed once, e.g during startup.</para>
        /// </summary>
        public void Execute(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return;

            var task = new BasicStatementTask(sql);
            Enqueue(task);
        }

        /// <summary>
        /// Delayed one-way statement methods.
        /// <para>Enqueues a one-way SQL operation in string format -with variable args- that will be executed asynchronously.</para>
        /// <para>This method should only be used for queries that are only executed once, e.g during startup.</para>
        /// </summary>
        public void PExecute(string format, params object?[] args)
        {
            var sql = string.Format(format, args);
            Execute(sql);
        }

        /// <summary>
        /// <para>Enqueues a one-way SQL operation in prepared statement format that will be executed asynchronously.</para>
        /// <para>Statement must be prepared with CONNECTION_ASYNC flag.</para>
        /// </summary>
        public void Execute(PreparedStatement stmt)
        {
            var task = new PreparedStatementTask(stmt);
            Enqueue(task);
        }

        /// <summary>
        /// Direct synchronous one-way statement methods.
        /// <para>Directly executes a one-way SQL operation in string format, that will block the calling thread until finished.</para>
        /// <para>This method should only be used for queries that are only executed once, e.g during startup.</para>
        /// </summary>
        public void DirectExecute(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return;

            var connection = GetFreeConnection();
            connection.Execute(sql);
            connection.Unlock();
        }

        /// <summary>
        /// Direct synchronous one-way statement methods.
        /// <para>Directly executes a one-way SQL operation in string format -with variable args-, that will block the calling thread until finished.</para>
        /// <para>This method should only be used for queries that are only executed once, e.g during startup.</para>
        /// </summary>
        public void DirectPExecute(string format, params object?[] args)
        {
            var sql = string.Format(format, args);
            DirectExecute(sql);
        }

        /// <summary>
        /// <para>Directly executes a one-way SQL operation in prepared statement format, that will block the calling thread until finished.</para>
        /// <para>Statement must be prepared with the CONNECTION_SYNCH flag.</para>
        /// </summary>
        public void DirectExecute(PreparedStatement stmt)
        {
            var connection = GetFreeConnection();
            connection.Execute(stmt);
            connection.Unlock();
        }

        /// <summary>
        /// Synchronous query (with QueryResult) methods.
        /// <para>Directly executes an SQL query in string format that will block the calling thread until finished.</para>
        /// </summary>
        public QueryResult? Query(string sql, T? connection = null)
        {
            if (connection == null)
                connection = GetFreeConnection();

            var result = connection.Query(sql);
            connection.Unlock();
            if (result == null || result.IsEmpty() || !result.NextRow())
            {
                if (result != null)
                    result.Dispose();

                return null;
            }

            return result;
        }

        /// <summary>
        /// Synchronous query (with QueryResult) methods.
        /// <para>Directly executes an SQL query in string format -with variable args- that will block the calling thread until finished.</para>
        /// </summary>
        public QueryResult? PQuery(string format, T? connection, params object?[] args)
        {
            var sql = string.Format(format, args);

            return Query(sql, connection);
        }

        /// <summary>
        /// <para>Directly executes an SQL query in prepared format that will block the calling thread until finished.</para>
        /// <para>Statement must be prepared with CONNECTION_SYNCH flag.</para>
        /// </summary>
        public PreparedQueryResult? Query(PreparedStatement stmt)
        {
            var connection = GetFreeConnection();
            var ret = connection.Query(stmt);
            connection.Unlock();

            if (ret == null || ret.IsEmpty())
            {
                return null;
            }

            return ret;
        }

        /// <summary>
        /// Enqueues a query in string format that will set the value of the QueryResult task return object as soon as the query is executed.
        /// The return value is then processed in ProcessQueryCallback methods.
        /// </summary>
        public QueryCallback AsyncQuery(string sql)
        {
            var task = new BasicStatementTask(sql, true);
            // Store future result before enqueueing - task might get already processed and deleted before returning from this method
            var result = task.GetFuture();
            Enqueue(task);

            if (result == null)
            {
                Assert(false);
                throw new Exception();
            }
            return new QueryCallback(result);
        }

        //! Enqueues a query in prepared format that will set the value of the PreparedQueryResultFuture return object as soon as the query is executed.
        //! The return value is then processed in ProcessQueryCallback methods.
        //! Statement must be prepared with CONNECTION_ASYNC flag.
        public QueryCallback AsyncQuery(PreparedStatement stmt)
        {
            var task = new PreparedStatementTask(stmt, true);
            // Store future result before enqueueing - task might get already processed and deleted before returning from this method
            var result = task.GetFuture();

            Enqueue(task);
            return new QueryCallback(result);
        }

        //! Enqueues a vector of SQL operations (can be both adhoc and prepared) that will set the value of the QueryResultHolderFuture
        //! return object as soon as the query is executed.
        //! The return value is then processed in ProcessQueryCallback methods.
        //! Any prepared statements added to this holder need to be prepared with the CONNECTION_ASYNC flag.
        public SqlQueryHolderCallback DelayQueryHolder(SqlQueryHolder<T> holder)
        {
            var task = new SqlQueryHolderTask(holder);
            // Store future result before enqueueing - task might get already processed and deleted before returning from this method
            var result = task.GetFuture();
            Enqueue(task);
            return new SqlQueryHolderCallback(holder, result);
        }

        /// <summary>
        /// Begins an transaction that will automatically rollback if not commited. (Autocommit=0)
        /// </summary>
        public SqlTransaction<T> BeginTransaction()
        {
            return new SqlTransaction<T>();
        }

        /// <summary>
        /// Enqueues a collection of one-way SQL operations (can be both adhoc and prepared). The order in which these operations
        /// were appended to the transaction will be respected during execution.
        /// </summary>
        public void CommitTransaction(SqlTransaction<T> transaction)
        {
#if DEBUG
            // Only analyze transaction weaknesses in Debug mode.
            // Ideally we catch the faults in Debug mode and then correct them,
            // so there's no need to waste these CPU cycles in Release mode.
            switch (transaction.GetSize())
            {
            case 0:
                FEL_LOG_DEBUG("sql.driver", "Transaction contains 0 queries. Not executing.");
                return;
            case 1:
                FEL_LOG_DEBUG("sql.driver", "Warning: Transaction only holds 1 query, consider removing Transaction context in code.");
                break;
            default:
                break;
            }
#endif
            Enqueue(new SqlTransactionTask(transaction));
        }

        /// <summary>
        /// Enqueues a collection of one-way SQL operations (can be both adhoc and prepared). The order in which these operations
        /// were appended to the transaction will be respected during execution.
        /// </summary>
        public SqlTransactionCallback AsyncCommitTransaction(SqlTransaction<T> transaction)
        {
#if DEBUG
            // Only analyze transaction weaknesses in Debug mode.
            // Ideally we catch the faults in Debug mode and then correct them,
            // so there's no need to waste these CPU cycles in Release mode.
            switch (transaction.GetSize())
            {
                case 0:
                    FEL_LOG_DEBUG("sql.driver", "Transaction contains 0 queries. Not executing.");
                    break;
                case 1:
                    FEL_LOG_DEBUG("sql.driver", "Warning: Transaction only holds 1 query, consider removing Transaction context in code.");
                    break;
                default:
                    break;
            }
#endif

            var task = new SqlTransactionWithResultTask(transaction);
            var result = task.GetFuture();
            Enqueue(task);
            return new SqlTransactionCallback(result);
        }

        /// <summary>
        /// Directly executes a collection of one-way SQL operations (can be both adhoc and prepared). The order in which these operations
        /// were appended to the transaction will be respected during execution.
        /// <summary>
        public void DirectCommitTransaction(SqlTransaction<T> transaction)
        {
            var connection = GetFreeConnection();
            var errorCode = connection.ExecuteTransaction(transaction);
            if (errorCode == MySqlErrorCode.None)
            {
                connection.Unlock(); // OK, operation succesful
                return;
            }

            // Handle MySQL Errno 1213 without extending deadlock to the core itself
            // @todo More elegant way
            if (errorCode == MySqlErrorCode.LockDeadlock)
            {
                //todo: handle multiple sync threads deadlocking in a similar way as async threads
                byte loopBreaker = 5;
                for (byte i = 0; i < loopBreaker; ++i)
                {
                    if (connection.ExecuteTransaction(transaction) == MySqlErrorCode.None)
                        break;
                }
            }

            //! Clean up now.
            transaction.CleanUp();

            connection.Unlock();
        }

        /// <summary>
        /// <para>Method used to execute ad-hoc sql in a diverse context.</para>
        /// <para>Will be wrapped in a transaction if valid object is present, otherwise executed standalone.</para>
        /// </summary>
        public void ExecuteOrAppend(SqlTransaction<T> trans, string sql)
        {
            if (trans == null)
                Execute(sql);
            else
                trans.Append(sql);
        }

        /// <summary>
        /// <para>Method used to execute ad-hoc statements in a diverse context.</para>
        /// <para>Will be wrapped in a transaction if valid object is present, otherwise executed standalone.</para>
        /// </summary>
        public void ExecuteOrAppend(SqlTransaction<T> trans, PreparedStatement stmt)
        {
            if (trans == null)
                Execute(stmt);
            else
                trans.Append(stmt);
        }

        private MySqlErrorCode OpenConnections(InternalIndex type, byte numConnections)
        {
            if (_connectionInfo == null)
            {
                Assert(false, "Connection info was not set!");
                return 0;
            }

            for (byte i = 0; i < numConnections; ++i)
            {
                // Create the connection
                var connection = new Func<MySqlConnectionProxy<Statements>?>(() => {

                    switch (type)
                    {
                        case IDX_ASYNC:
                            return new MySqlConnectionProxy<Statements>(_queue, _connectionInfo);
                        case IDX_SYNCH:
                            return new MySqlConnectionProxy<Statements>(_connectionInfo);
                        default:
                            Environment.FailFast(null);
                            break;
                    }

                    return null;
                })();

                if (connection == null)
                {
                    Assert(false);
                    return 0;
                }

                var error = connection.Open();
                if (error != MySqlErrorCode.None)
                {
                    // Failed to open a connection or invalid version, abort and cleanup
                    _connections[(int)type].Clear();
                    return error;
                }
                else
                {
                    _connections[(int)type].Add((T)connection);
                }
            }

            // Everything is fine
            return 0;
        }

        private void Enqueue(SqlOperation op)
        {
            _queue.Push(op);
        }

        /// <summary>
        /// <para>Gets a free connection in the synchronous connection pool.</para>
        /// <para>Caller MUST call t.Unlock() after touching the MySQL context to prevent deadlocks.</para>
        /// </summary>
        private T GetFreeConnection()
        {
#if DEBUG
            if (_warnSyncQueries.Value)
                FEL_LOG_WARN("sql.performances", "Sync query at:{0}{1}", Environment.NewLine, Environment.StackTrace);
#endif

            byte i = 0;
            var num_cons = _connections[(int)IDX_SYNCH].Count;
            T? connection = null;
            //! Block forever until a connection is free
            for (;;)
            {
                connection = _connections[(int)IDX_SYNCH][++i % num_cons];
                //! Must be matched with t->Unlock() or you will get deadlocks
                if (connection.LockIfReady())
                    break;
            }

            return connection;
        }

        public MySqlErrorCode Open()
        {
            if (_connectionInfo == null)
            {
                Assert(false, "Connection info was not set!");
                return 0;
            }

            FEL_LOG_INFO("sql.driver", "Opening DatabasePool '{0}'. Asynchronous connections: {1}, synchronous connections: {2}.", GetDatabaseName(), _async_threads, _synch_threads);

            var error = OpenConnections(IDX_ASYNC, _async_threads);

            if (error != 0)
                return error;

            error = OpenConnections(IDX_SYNCH, _synch_threads);

            if (error == 0)
            {
                FEL_LOG_INFO("sql.driver", "DatabasePool '{0}' opened successfully. {1} total connections running.",
                    GetDatabaseName(), _connections[(int)IDX_SYNCH].Count + _connections[(int)IDX_ASYNC].Count);
            }

            return error;
        }

        public void Close()
        {
            FEL_LOG_INFO("sql.driver", "Closing down DatabasePool '{0}'.", GetDatabaseName());

            //! Closes the actualy MySQL connection.
            foreach(var conn in _connections[(int)IDX_ASYNC])
                conn.Dispose();
            _connections[(int)IDX_ASYNC].Clear();

            FEL_LOG_INFO("sql.driver", "Asynchronous connections on DatabasePool '{0}' terminated. Proceeding with synchronous connections.", GetDatabaseName());

            //! Shut down the synchronous connections
            //! There's no need for locking the connection, because DatabaseWorkerPool<>::Close
            //! should only be called after any other thread tasks in the core have exited,
            //! meaning there can be no concurrent access at this point.
            foreach(var conn in _connections[(int)IDX_SYNCH])
                conn.Dispose();
            _connections[(int)IDX_SYNCH].Clear();

            FEL_LOG_INFO("sql.driver", "All connections on DatabasePool '{0}' closed.", GetDatabaseName());
        }

        /// <summary>
        /// Apply escape string'ing for current collation. (utf8)
        /// </summary>
        public void EscapeString(ref string str)
        {
            _connections[(int)IDX_SYNCH][0].EscapeString(ref str);
        }

        //! Prepares all prepared statements
        public bool PrepareStatements()
        {
            foreach (var connections in _connections)
            {
                foreach (var connection in connections)
                {
                    connection.LockIfReady();
                    if (!connection.PrepareStatements())
                    {
                        connection.Unlock();
                        Close();
                        return false;
                    }
                    else
                        connection.Unlock();
                }
            }

            return true;
        }

        public MySqlConnectionInfo? GetConnectionInfo()
        {
            return _connectionInfo;
        }

        /// <summary>
        /// Keeps all our MySQL connections alive, prevent the server from disconnecting us.
        /// </summary>
        public void KeepAlive()
        {
            //! Ping synchronous connections
            foreach (var connection in _connections[(int)IDX_SYNCH])
            {
                if (connection.LockIfReady())
                {
                    connection.Ping();
                    connection.Unlock();
                }
            }

            //! Assuming all worker threads are free, every worker thread will receive 1 ping operation request
            //! If one or more worker threads are busy, the ping operations will not be split evenly, but this doesn't matter
            //! as the sole purpose is to prevent connections from idling.
            var count = _connections[(int)IDX_ASYNC].Count;
            for (var i = 0; i < count; ++i)
                Enqueue(new PingOperation());
        }

        public void WarnAboutSyncQueries(bool warn)
        {
#if DEBUG
            _warnSyncQueries.Value = warn;
#endif
        }

        /// <summary>
        /// Get a prepared statement object for usage in upper level code.
        /// </summary>
        public PreparedStatement GetPreparedStatement(Statements index)
        {
            return _connections[(int)IDX_SYNCH][0].GetPreparedStatement(index);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _queue.Cancel();
            }

            _disposed = true;
        }
    }
}
