// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System.Collections.Generic;
using System;
using System.Threading;
using MySqlConnector;
using Common;
using static Common.Log;
using static Common.Errors;
using static Common.Util;
using static Server.Database.SqlElementDataType;

namespace Server.Database
{
    public class SqlTransactionBase
    {
        bool _cleanedUp;

        protected List<SqlElementData> _queries = new List<SqlElementData>();
        public List<SqlElementData> Queries => _queries;

        protected void AppendPreparedStatement(PreparedStatementBase stmt)
        {
            SqlElementData data = new SqlElementData();
            data.Type = SQL_ELEMENT_PREPARED;
            data.Element.Stmt = stmt;
            _queries.Add(data);
        }

        public void CleanUp()
        {
            // This might be called by explicit calls to Cleanup or by the auto-destructor
            if (_cleanedUp)
                return;

            _queries.Clear();
            _cleanedUp = true;
        }

        public int GetSize() { return _queries.Count; }

        public void Append(string sql)
        {
            SqlElementData data = new SqlElementData();
            data.Type = SQL_ELEMENT_RAW;
            data.Element.Query = sql;

            _queries.Add(data);
        }

        public void PAppend(string format, params object?[] args)
        {
            Append(string.Format(format, args));
        }
    }

    public class SqlTransaction<T> : SqlTransactionBase where T : MySqlConnectionProxyBase
    {
        public void Append(PreparedStatement<T> statement)
        {
            AppendPreparedStatement(statement);
        }
    }

    class SqlTransactionTask : SqlOperation
    {
        public SqlTransactionTask(SqlTransactionBase trans)
        {
            _trans = trans;
        }

        public const int DEADLOCK_MAX_RETRY_TIME_MS = 60000;

        protected new virtual bool Execute()
        {
            MySqlErrorCode errorCode = TryExecute();
            if (errorCode == MySqlErrorCode.None)
                return true;

            if (errorCode == MySqlErrorCode.LockDeadlock)
            {
                // Make sure only 1 async thread retries a transaction so they don't keep dead-locking each other
                lock (_deadlockLock)
                {
                    // Handle MySQL Errno 1213 without extending deadlock to the core itself
                    var startTime = Time.Now;
                    for (int loopDuration = 0; loopDuration <= DEADLOCK_MAX_RETRY_TIME_MS; loopDuration = (int)(Time.Now - startTime).TotalMilliseconds)
                    {
                        if (TryExecute() == MySqlErrorCode.None)
                            return true;

                        FEL_LOG_WARN("sql.sql", "Deadlocked SQL Transaction, retrying. Loop timer: {0} ms, Thread Id: {1}", loopDuration, Thread.CurrentThread.ManagedThreadId);
                    }

                    FEL_LOG_ERROR("sql.sql", "Fatal deadlocked SQL Transaction, it will not be retried anymore. Thread Id: {0}", Thread.CurrentThread.ManagedThreadId);
                }
            }

            // Clean up now.
            CleanUpOnFailure();

            return false;
        }

        public MySqlErrorCode TryExecute()
        {
            if (Conn == null)
            {
                Assert(false);    
                return MySqlErrorCode.None;
            }

            return Conn.ExecuteTransaction(_trans);
        }

        public void CleanUpOnFailure()
        {
            _trans.CleanUp();
        }

        SqlTransactionBase _trans;
        public static object _deadlockLock = new object();
    }

    class SqlTransactionWithResultTask : SqlTransactionTask
    {
        public SqlTransactionWithResultTask(SqlTransactionBase trans) : base(trans) { }

        protected override bool Execute()
        {
            MySqlErrorCode errorCode = TryExecute();
            if (errorCode == MySqlErrorCode.None)
            {
                _result.SetResult(true);
                return true;
            }

            if (errorCode == MySqlErrorCode.LockDeadlock)
            {
                // Make sure only 1 async thread retries a transaction so they don't keep dead-locking each other
                lock (_deadlockLock)
                {
                    // Handle MySQL Errno 1213 without extending deadlock to the core itself
                    var startTime = Time.Now;
                    for (int loopDuration = 0; loopDuration <= DEADLOCK_MAX_RETRY_TIME_MS; loopDuration = (int)(Time.Now - startTime).TotalMilliseconds)
                    {
                        if (TryExecute() == MySqlErrorCode.None)
                        {
                            _result.SetResult(true);
                            return true;
                        }

                        FEL_LOG_WARN("sql.sql", "Deadlocked SQL Transaction, retrying. Loop timer: {0} ms, Thread Id: {1}", loopDuration, Thread.CurrentThread.ManagedThreadId);
                    }

                    FEL_LOG_ERROR("sql.sql", "Fatal deadlocked SQL Transaction, it will not be retried anymore. Thread Id: {0}", Thread.CurrentThread.ManagedThreadId);
                }
            }

            // Clean up now.
            CleanUpOnFailure();

            _result.SetResult(false);
            return false;
        }

        public Future<bool> GetFuture() { return _result.GetFuture(); }

        Promise<bool> _result = new();
    }

    public class SqlTransactionCallback : ISqlCallback
    {
        public SqlTransactionCallback(Future<bool> future)
        {
            _future = future;
        }

        public void AfterComplete(Action<bool> callback)
        {
            _callback = callback;
        }

        public bool InvokeIfReady()
        {
            if (_future.Valid && _future.Wait(0))
            {
                if (_callback != null)
                    _callback(_future.Result);

                return true;
            }

            return false;
        }

        Future<bool> _future;
        Action<bool>? _callback;
    }
}
