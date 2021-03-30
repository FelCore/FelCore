// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using Common;
using static Common.Errors;

namespace Server.Database
{
    public class BasicStatementTask : SqlOperation
    {
        public BasicStatementTask(string sql, bool hasResult = false)
        {
            _sql = sql;
            _hasResult = hasResult;

            _result = new Promise<QueryResult?>();
        }

        string _sql; //- Raw query to be executed
        bool _hasResult;

        Promise<QueryResult?> _result;

        public Future<QueryResult?> GetFuture()
        {
            Assert(_hasResult, "BasicStatementTask has no result!");
            return _result.GetFuture();
        }

        public override bool Execute()
        {
            if (Conn == null)
                return false;

            if (_hasResult)
            {
                var result = Conn.Query(_sql);
                if (result == null || !result.NextRow())
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
