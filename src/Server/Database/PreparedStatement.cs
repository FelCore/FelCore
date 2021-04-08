// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Buffers;
using Common;
using static Common.Errors;

namespace Server.Database
{
    public class PreparedStatementBase : IDisposable
    {
        private int _index;
        public int Index => _index;

        private object?[] _parameters;
        public object?[] Parameters => _parameters;

        private byte _parameterCount;
        public byte ParameterCount => _parameterCount;

        public PreparedStatementBase(int index, byte paramCount)
        {
            _index = index;

            _parameterCount = paramCount;
            _parameters = ArrayPool<object?>.Shared.Rent(_parameterCount);
        }

        public void Clear()
        {
            for (byte i = 0; i < _parameterCount; i++)
                _parameters[i] = null;
        }

        ~PreparedStatementBase()
        {
            Dispose(false);
        }

        bool _disposed;
        public bool Disposed => _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }

                ArrayPool<object?>.Shared.Return(_parameters);
            }
            _disposed = true;
        }
    }

    public class PreparedStatement<T> : PreparedStatementBase where T : MySqlConnection 
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

                if (result == null || result.GetRowCount() == 0)
                {
                    result?.Dispose();
                    _result.SetResult(null);
                    return false;
                }

                _result.SetResult(result);
                return true;
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
