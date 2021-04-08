// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using Common;
using static Common.Log;
using static Common.Errors;

namespace Server.Database
{
    public class SqlQueryHolderBase
    {
        (PreparedStatementBase, PreparedQueryResult?)[] _queries = Array.Empty<(PreparedStatementBase, PreparedQueryResult?)>();

        public (PreparedStatementBase, PreparedQueryResult?)[] Queries => _queries;

        public void SetSize(int size)
        {
            Array.Resize(ref _queries, size);
        }
        protected bool SetPreparedQueryImpl(int index, PreparedStatementBase stmt)
        {
            if (_queries.Length <= index)
            {
                FEL_LOG_ERROR("sql.sql", "Query index ({0}) out of range (size: {1}) for prepared statement", index, _queries.Length);
                return false;
            }

            _queries[index].Item1 = stmt;
            return true;
        }

        public PreparedQueryResult? GetPreparedResult(int index)
        {
            // Don't call to this function if the index is of a prepared statement
            Assert(index < _queries.Length, string.Format("Query holder result index out of range, tried to access index {0} but there are only {1} results", index, _queries.Length));

            return _queries[index].Item2;
        }

        public void SetPreparedResult(int index, PreparedQueryResult? result)
        {
            if (result != null && result.GetRowCount() == 0)
            {
                result.Dispose();
                result = null;
            }

            /// store the result in the holder
            if (index < _queries.Length)
                _queries[index].Item2 = result;
        }
    }

    public class SqlQueryHolder<T> : SqlQueryHolderBase where T : MySqlConnection
    {
        public bool SetPreparedQuery(int index, PreparedStatement<T> stmt)
        {
            return SetPreparedQueryImpl(index, stmt);
        }
    }

    public class SqlQueryHolderTask : SqlOperation
    {
        SqlQueryHolderBase _holder;
        Promise<SqlQueryHolderBase> _result;

        public SqlQueryHolderTask(SqlQueryHolderBase holder)
        {
            _holder = holder;
            _result = new Promise<SqlQueryHolderBase>();
        }

        public override bool Execute()
        {
            if (Conn == null)
                return false;

            /// execute all queries in the holder and pass the results
            for (int i = 0; i < _holder.Queries.Length; ++i)
            {
                var stmt = _holder.Queries[i].Item1;
                if (stmt != null)
                    _holder.SetPreparedResult(i, Conn.Query(stmt));
            }

            _result.SetResult(_holder);
            return true;
        }

        public Future<SqlQueryHolderBase> GetFuture() { return _result.GetFuture(); }
    }

    public class SqlQueryHolderCallback : ISqlCallback
    {
        SqlQueryHolderBase _holder;
        Future<SqlQueryHolderBase> _future;
        Action<SqlQueryHolderBase>? _callback;

        public SqlQueryHolderCallback(SqlQueryHolderBase holder, Future<SqlQueryHolderBase> future)
        {
            _holder = holder;
            _future = future;
        }

        public void AfterComplete(Action<SqlQueryHolderBase> callback)
        {
            _callback = callback;
        }

        public bool InvokeIfReady()
        {
            if (_future.Valid && _future.Wait(0))
            {
                if (_callback != null)
                    _callback(_holder);

                return true;
            }

            return false;
        }
    }
}
