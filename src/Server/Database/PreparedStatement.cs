// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using Common;
using static Common.Errors;

namespace Server.Database
{
    public class PreparedStatementBase
    {
        private int _index;
        public int Index => _index;

        public readonly object?[] Parameters;

        private byte _parameterCount;
        public byte ParameterCount => _parameterCount;

        private string? _commandText;
        public string? CommandText => _commandText;


        public PreparedStatementBase(int index, byte parameterCount)
        {
            _index = index;

            _parameterCount = parameterCount;
            Parameters = new object?[_parameterCount];
        }

        public void Bind(PreparedStatementQuery preparedQuery)
        {
            _commandText = preparedQuery.Sql;
        }

        public void SetValue(int index, object value)
        {
            Parameters[index] = value;
        }

        public void Clear()
        {
            for (byte i = 0; i < _parameterCount; i++)
                Parameters[i] = null;
        }

        public string GetQueryString()
        {
            if (string.IsNullOrEmpty(_commandText))
                return string.Empty;

            var sb = new StringBuilder(_commandText);
            int startIndex = 0;
            foreach(var val in Parameters)
            {
                var index = sb.ToString().IndexOf('?', startIndex);
                if (index != -1)
                {
                    startIndex = index;

                    sb.Remove(index, 1);
                    sb.Insert(index, $"'{val}'");
                }
            }

            return sb.ToString();
        }
    }

    public class PreparedStatement<T> : PreparedStatementBase where T : MySqlConnectionProxyBase 
    {
        public PreparedStatement(int index, byte parameterCount) : base(index, parameterCount)
        {
        }
    }

    public class PreparedStatementTask : SqlOperation
    {
        PreparedStatementBase _stmt;
        bool _hasResult;
        Promise<PreparedQueryResult?> _result;

        public PreparedStatementTask(PreparedStatementBase stmt, bool hasResult = false)
        {
            _stmt = stmt;
            _hasResult = hasResult;

            _result = new Promise<PreparedQueryResult?>();
        }

        public override bool Execute()
        {
            if (Conn == null)
                return false;

            if (_hasResult)
            {
                var result = Conn.Query(_stmt);

                _result.SetResult(result);
                return result != null;
            }

            return Conn.Execute(_stmt);
        }

        public Future<PreparedQueryResult?> GetFuture()
        {
            Assert(_hasResult, "PreparedStatementTask has no result!");
            return _result.GetFuture();
        }
    }
}
