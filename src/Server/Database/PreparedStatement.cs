// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Threading.Tasks;
using static Common.Errors;

namespace Server.Database
{
    public class PreparedStatement
    {
        public readonly string CommandText;
        public readonly object?[] Parameters;

        private int _parameterCount;
        public int ParameterCount => _parameterCount;

        public PreparedStatement(string commandText, int parameterCount)
        {
            CommandText = commandText;

            _parameterCount = parameterCount;
            Parameters = new object?[_parameterCount];
        }

        public void SetValue(int index, object value)
        {
            Parameters[index] = value;
        }

        public void Clear()
        {
            for (var i = 0; i < _parameterCount; i++)
                Parameters[i] = null;
        }

        public string GetQueryString()
        {
            var sb = new StringBuilder(CommandText);
            int startIndex = 0;
            foreach(var val in Parameters)
            {
                var index = CommandText.IndexOf('?', startIndex);
                if (index != -1)
                {
                    startIndex = index;

                    sb.Remove(index, 1);
                    sb.Insert(index, val);
                }
            }

            return sb.ToString();
        }
    }

    public class PreparedStatementTask : SqlOperation
    {
        PreparedStatement _stmt;
        TaskCompletionSource<PreparedQueryResult?>? _result;

        public PreparedStatementTask(PreparedStatement stmt, bool hasResult = false)
        {
            _stmt = stmt;

            if (hasResult)
                _result = new TaskCompletionSource<PreparedQueryResult?>();
        }

        public override bool Execute()
        {
            if (Conn == null)
                return false;

            if (_result != null)
            {
                var result = Conn.Query(_stmt);

                _result.SetResult(result);
                return result != null && !result.IsEmpty();
            }

            return Conn.Execute(_stmt);
        }

        public Task<PreparedQueryResult?> GetFuture()
        {
            if (_result == null)
            {
                Assert(false);
                throw new Exception();
            }
            return _result.Task;
        }
    }
}
