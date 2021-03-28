// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using MySqlConnector;
using MySqlConnector.Core;
using static Common.Errors;

namespace Server.Database
{
    public class QueryResult : IDisposable
    {
        MySqlDataReader? _reader;

        public MySqlDataReader Reader
        {
            get
            {
                if (_reader == null)
                {
                    Assert(false, "Could not reader field reader when sql result is empty!");
                    throw new Exception();
                }

                return _reader;
            }
        }

        Row? _currentRow;

        bool _disposed;
        public bool Disposed => _disposed;

        private QueryResult() {}

        public QueryResult(MySqlDataReader reader)
        {
            _reader = reader;
        }

        public bool IsNull(int column)
        {
            if (_reader == null)
            {
                Assert(false);
                return default;
            }

            return _reader.IsDBNull(column);
        }

        public int GetFieldCount() { return _reader == null ? 0 : _reader.FieldCount; }

        public bool NextRow()
        {
            if (_reader == null)
                return false;

            if (_reader.Read())
            {
                _currentRow = _reader.GetResultSet().GetCurrentRow();
                return true;
            }

            _reader.Close();
            return false;
        }

        public Row? Fetch()
        {
            return _currentRow;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (_reader != null)
                {
                    _reader.Close();
                    _reader = null;
                }
            }

            _disposed = true;
        }
    }

    public class PreparedQueryResult
    {
        int _rowCount;
        int _rowPosition;
        int _fieldCount;

        List<Row> _rows = new List<Row>();

        public PreparedQueryResult() {}

        public PreparedQueryResult(MySqlDataReader reader)
        {
            _fieldCount = reader.FieldCount;

            while(reader.Read())
            {
                _rowCount++;
                _rows.Add(reader.GetResultSet().GetCurrentRow());
            }

            reader.Close();
        }

        public bool IsNull(int column)
        {
            return _rows[_rowPosition].IsDBNull(column);
        }

        public int GetRowCount() { return _rowCount; }

        public int GetFieldCount() { return _fieldCount; }

        public bool NextRow()
        {
            /// Only updates the m_rowPosition so upper level code knows in which element
            /// of the rows vector to look
            if (++_rowPosition >= _rowCount)
                return false;

            return true;
        }

        public Row Fetch()
        {
            Assert(_rowPosition < _rowCount);
            return _rows[_rowPosition];
        }
    }
}
