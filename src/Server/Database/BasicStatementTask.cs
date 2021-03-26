// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading.Tasks;
using static Common.Errors;

namespace Server.Database
{
    public class BasicStatementTask : SqlOperation
    {
        public BasicStatementTask(string sql, bool hasResult = false)
        {
            _sql = sql;

            if (hasResult)
                _result = new TaskCompletionSource<QueryResult?>();
        }

        string _sql; //- Raw query to be executed
        TaskCompletionSource<QueryResult?>? _result;

        public Task<QueryResult?> GetFuture()
        {
            if (_result == null)
            {
                Assert(false);
                throw new Exception();
            }
            return _result.Task;
        }

        public override bool Execute()
        {
            if (Conn == null)
                return false;

            if (_result != null)
            {
                var result = Conn.Query(_sql);
                if (result == null || result.IsEmpty() || !result.NextRow())
                {
                    if (result != null)
                        result.Dispose();

                    _result.SetResult(null);
                    return false;
                }

                _result.SetResult(result);
                return true;
            }

            return Conn.Execute(_sql);
        }
    }
}
