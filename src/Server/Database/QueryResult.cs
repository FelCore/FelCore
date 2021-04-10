// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using MySqlSharp;
using static Common.Log;
using static Common.Errors;
using static MySqlSharp.NativeMethods;

namespace Server.Database
{
    using static _MySqlHelper;
    using static enum_field_types;

    public unsafe static class _MySqlHelper
    {
        public static int SizeForType(MYSQL_FIELD* field)
        {
            switch (field->type)
            {
                case MYSQL_TYPE_NULL:
                    return 0;
                case MYSQL_TYPE_TINY:
                    return 1;
                case MYSQL_TYPE_YEAR:
                case MYSQL_TYPE_SHORT:
                    return 2;
                case MYSQL_TYPE_INT24:
                case MYSQL_TYPE_LONG:
                case MYSQL_TYPE_FLOAT:
                    return 4;
                case MYSQL_TYPE_DOUBLE:
                case MYSQL_TYPE_LONGLONG:
                case MYSQL_TYPE_BIT:
                    return 8;

                case MYSQL_TYPE_TIMESTAMP:
                case MYSQL_TYPE_DATE:
                case MYSQL_TYPE_TIME:
                case MYSQL_TYPE_DATETIME:
                    return sizeof(MYSQL_TIME);

                case MYSQL_TYPE_TINY_BLOB:
                case MYSQL_TYPE_MEDIUM_BLOB:
                case MYSQL_TYPE_LONG_BLOB:
                case MYSQL_TYPE_BLOB:
                case MYSQL_TYPE_STRING:
                case MYSQL_TYPE_VAR_STRING:
                    return (int)(field->max_length) + 1;

                case MYSQL_TYPE_DECIMAL:
                case MYSQL_TYPE_NEWDECIMAL:
                    return 64;

                case MYSQL_TYPE_GEOMETRY:
                    /*
                    Following types are not sent over the wire:
                    MYSQL_TYPE_ENUM:
                    MYSQL_TYPE_SET:
                    */
                default:
                    FEL_LOG_WARN("sql.sql", "SQL::SizeForType(): invalid field type {0}", field->type);
                    return 0;
            }
        }

        public static DatabaseFieldTypes MySqlTypeToFieldType(enum_field_types type)
        {
            switch (type)
            {
                case MYSQL_TYPE_NULL:
                    return DatabaseFieldTypes.Null;
                case MYSQL_TYPE_TINY:
                    return DatabaseFieldTypes.Int8;
                case MYSQL_TYPE_YEAR:
                case MYSQL_TYPE_SHORT:
                    return DatabaseFieldTypes.Int16;
                case MYSQL_TYPE_INT24:
                case MYSQL_TYPE_LONG:
                    return DatabaseFieldTypes.Int32;
                case MYSQL_TYPE_LONGLONG:
                case MYSQL_TYPE_BIT:
                    return DatabaseFieldTypes.Int64;
                case MYSQL_TYPE_FLOAT:
                    return DatabaseFieldTypes.Float;
                case MYSQL_TYPE_DOUBLE:
                    return DatabaseFieldTypes.Double;
                case MYSQL_TYPE_DECIMAL:
                case MYSQL_TYPE_NEWDECIMAL:
                    return DatabaseFieldTypes.Decimal;
                case MYSQL_TYPE_TIMESTAMP:
                case MYSQL_TYPE_DATE:
                case MYSQL_TYPE_TIME:
                case MYSQL_TYPE_DATETIME:
                    return DatabaseFieldTypes.Date;
                case MYSQL_TYPE_TINY_BLOB:
                case MYSQL_TYPE_MEDIUM_BLOB:
                case MYSQL_TYPE_LONG_BLOB:
                case MYSQL_TYPE_BLOB:
                case MYSQL_TYPE_STRING:
                case MYSQL_TYPE_VAR_STRING:
                    return DatabaseFieldTypes.Binary;
                default:
                    FEL_LOG_WARN("sql.sql", "MysqlTypeToFieldType(): invalid field type {0}", type);
                    break;
            }

            return DatabaseFieldTypes.Null;
        }

        public static void InitializeDatabaseFieldMetadata(QueryResultFieldMetadata* meta, MYSQL_FIELD* field, int fieldIndex)
        {
            meta->TableName = (IntPtr)field->org_table;
            meta->TableAlias = (IntPtr)field->table;
            meta->Name = (IntPtr)field->org_name;
            meta->Alias = (IntPtr)field->name;
            meta->FieldType = field->type;
            meta->Index = fieldIndex;
            meta->Type = MySqlTypeToFieldType(field->type);
        }
    }

    public unsafe class QueryResult : IDisposable
    {
        MYSQL_RES* _result;
        MYSQL_FIELD* _fields;
        QueryResultFieldMetadata* _fieldMetadata;
        long _rowCount;
        Field* _currentRow;
        int _fieldCount;

        bool _disposed;
        public bool Disposed => _disposed;

        private QueryResult() {}

        public QueryResult(MYSQL_RES* result, MYSQL_FIELD* fields, long rowCount, int fieldCount)
        {
            _result = result;
            _fields = fields;
            _rowCount = rowCount;
            _fieldCount = fieldCount;

            _fieldMetadata = (QueryResultFieldMetadata*)Marshal.AllocHGlobal(sizeof(QueryResultFieldMetadata) * _fieldCount);
            _currentRow = (Field*)Marshal.AllocHGlobal(sizeof(Field) * _fieldCount);

            for (int i = 0; i < _fieldCount; i++)
            {
                InitializeDatabaseFieldMetadata(&_fieldMetadata[i], &_fields[i], i);
                _currentRow[i].SetMetadata(&_fieldMetadata[i]);
            }
        }

        ~QueryResult()
        {
            Dispose(false);
        }

        public bool NextRow()
        {
            IntPtr* row;

            if (_result == default)
                return false;

            row = mysql_fetch_row(_result);
            if (row == default)
            {
                CleanUp();
                return false;
            }

            UIntPtr* lengths = mysql_fetch_lengths(_result);
            if (lengths == default)
            {
                FEL_LOG_WARN("sql.sql", "{0}:mysql_fetch_lengths, cannot retrieve value lengths. Error {1}.", "QueryResult::NextRow()", mysql_error(_result->handle));
                CleanUp();
                return false;
            }

            for (int i = 0; i < _fieldCount; i++)
                _currentRow[i].SetStructuredValue((byte*)row[i], (int)lengths[i]);

            return true;
        }

        public long GetRowCount() { return _rowCount; }
        public int GetFieldCount() { return _fieldCount; }

        public ReadOnlySpan<Field> Fetch()
        {
            return new ReadOnlySpan<Field>(_currentRow, _fieldCount);
        }

        void CleanUp()
        {
            if (_currentRow != default)
            {
                Marshal.FreeHGlobal((IntPtr)_currentRow);
                _currentRow = default;
            }

            if (_fieldMetadata != default)
            {
                Marshal.FreeHGlobal((IntPtr)_fieldMetadata);
                _fieldMetadata = default;
            }

            if (_result != default)
            {
                mysql_free_result(_result);
                _result = default;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
            }

            CleanUp();

            _disposed = true;
        }
    }

    public unsafe class PreparedQueryResult : IDisposable
    {
        QueryResultFieldMetadata* _fieldMetadata;
        Field* _rows;
        long _rowCount;
        long _rowPosition;
        int _fieldCount;
        MYSQL_BIND* _rBind;
        MYSQL_STMT* _stmt;
        MYSQL_RES* _metadataResult;    ///< Field metadata, returned by mysql_stmt_result_metadata

        private PreparedQueryResult() {}

        public const int UNSIGNED_FLAG = 32;    /**< Field is unsigned */

        public PreparedQueryResult(MYSQL_STMT* stmt, MYSQL_RES* result, long rowCount, int fieldCount)
        {
            _stmt = stmt;
            _rowCount = rowCount;
            _fieldCount = fieldCount;
            _metadataResult = result;

            if (_metadataResult == default)
                return;

            if (_stmt->bind_result_done != 0)
            {
                Marshal.FreeHGlobal((IntPtr)stmt->bind->length);
                Marshal.FreeHGlobal((IntPtr)stmt->bind->is_null);
            }

            _rBind = (MYSQL_BIND*)Marshal.AllocHGlobal(sizeof(MYSQL_BIND) * _fieldCount);

            //- for future readers wondering where the fuck this is freed - mysql_stmt_bind_result moves pointers to these
            // from m_rBind to m_stmt->bind and it is later freed by the `if (m_stmt->bind_result_done)` block just above here
            // MYSQL_STMT lifetime is equal to connection lifetime
            var isNullBuffer = (bool*)Marshal.AllocHGlobal(sizeof(bool) * _fieldCount);
            var lengthBuffer = (UIntPtr*)Marshal.AllocHGlobal(sizeof(UIntPtr) * _fieldCount);

            new Span<byte>(_rBind, sizeof(MYSQL_BIND) * _fieldCount).Fill(0);
            new Span<byte>(isNullBuffer, sizeof(bool) * _fieldCount).Fill(0);
            new Span<byte>(lengthBuffer, sizeof(UIntPtr) * _fieldCount).Fill(0);

            //- This is where we store the (entire) resultset
            if (mysql_stmt_store_result(_stmt) != 0)
            {
                FEL_LOG_WARN("sql.sql", "{0}:mysql_stmt_store_result, cannot bind result from MySQL server. Error: {1}", "PreparedQueryResult()", mysql_stmt_error(_stmt));
                Marshal.FreeHGlobal((IntPtr)_rBind);
                Marshal.FreeHGlobal((IntPtr)isNullBuffer);
                Marshal.FreeHGlobal((IntPtr)lengthBuffer);
                _rBind = default;
                return;
            }

            _rowCount = mysql_stmt_num_rows(_stmt);

            //- This is where we prepare the buffer based on metadata
            MYSQL_FIELD* field = mysql_fetch_fields(_metadataResult);

            _fieldMetadata = (QueryResultFieldMetadata*)Marshal.AllocHGlobal(sizeof(QueryResultFieldMetadata) * _fieldCount);

            int rowSize = 0;
            for (int i = 0; i < _fieldCount; ++i)
            {
                int size = SizeForType(&field[i]);
                rowSize += size;

                InitializeDatabaseFieldMetadata(&_fieldMetadata[i], &field[i], i);

                _rBind[i].buffer_type = field[i].type;
                _rBind[i].buffer_length = (UIntPtr)size;
                _rBind[i].length = &lengthBuffer[i];
                _rBind[i].is_null = &isNullBuffer[i];
                _rBind[i].error = default;
                _rBind[i].is_unsigned = (field[i].flags & UNSIGNED_FLAG) != 0;
            }

            var dataBuffer = (byte*)Marshal.AllocHGlobal(rowSize * (int)_rowCount);
            for (int i = 0, offset = 0; i < _fieldCount; ++i)
            {
                _rBind[i].buffer = dataBuffer + offset;
                offset += (int)_rBind[i].buffer_length;
            }

            //- This is where we bind the bind the buffer to the statement
            if (mysql_stmt_bind_result(_stmt, _rBind))
            {
                FEL_LOG_WARN("sql.sql", "{0}:mysql_stmt_bind_result, cannot bind result from MySQL server. Error: {1}", "PreparedQueryResult()", mysql_stmt_error(_stmt));
                mysql_stmt_free_result(_stmt);
                CleanUp();
                Marshal.FreeHGlobal((IntPtr)isNullBuffer);
                Marshal.FreeHGlobal((IntPtr)lengthBuffer);
                return;
            }

            _rows = (Field*)Marshal.AllocHGlobal(sizeof(Field) * _fieldCount * (int)_rowCount);

            while (_NextRow())
            {
                for (int fIndex = 0; fIndex < _fieldCount; ++fIndex)
                {
                    _rows[(int)_rowPosition * _fieldCount + fIndex].SetMetadata(&_fieldMetadata[fIndex]);

                    var buffer_length = (int)_rBind[fIndex].buffer_length;
                    var fetched_length = (int)(*_rBind[fIndex].length);
                    if (!*_rBind[fIndex].is_null)
                    {
                        void* buffer = _stmt->bind[fIndex].buffer;
                        switch (_rBind[fIndex].buffer_type)
                        {
                            case MYSQL_TYPE_TINY_BLOB:
                            case MYSQL_TYPE_MEDIUM_BLOB:
                            case MYSQL_TYPE_LONG_BLOB:
                            case MYSQL_TYPE_BLOB:
                            case MYSQL_TYPE_STRING:
                            case MYSQL_TYPE_VAR_STRING:
                                // warning - the string will not be null-terminated if there is no space for it in the buffer
                                // when mysql_stmt_fetch returned MYSQL_DATA_TRUNCATED
                                // we cannot blindly null-terminate the data either as it may be retrieved as binary blob and not specifically a string
                                // in this case using Field::GetCString will result in garbage
                                if (fetched_length < buffer_length)
                                    *((byte*)buffer + fetched_length) = 0;
                                break;
                            default:
                                break;
                        }

                        _rows[(int)_rowPosition * _fieldCount + fIndex].SetByteValue((byte*)buffer, fetched_length);

                        // move buffer pointer to next part
                        _stmt->bind[fIndex].buffer = (byte*)buffer + rowSize;
                    }
                    else
                    {
                        _rows[(int)_rowPosition * _fieldCount + fIndex].SetByteValue(default, (int)(*_rBind[fIndex].length));
                    }
                }
                _rowPosition++;
            }

            _rowPosition = 0;

            /// All data is buffered, let go of mysql c api structures
            mysql_stmt_free_result(_stmt);
        }

        ~PreparedQueryResult()
        {
            Dispose(false);
        }

        public long GetRowCount() { return _rowCount; }

        public int GetFieldCount() { return _fieldCount; }

        public bool NextRow()
        {
            /// Only updates the m_rowPosition so upper level code knows in which element
            /// of the rows vector to look
            if (++_rowPosition >= _rowCount)
                return false;

            return true;
        }

        public const int MYSQL_NO_DATA = 100;
        public const int MYSQL_DATA_TRUNCATED = 101;

        bool _NextRow()
        {
            /// Only called in low-level code, namely the constructor
            /// Will iterate over every row of data and buffer it
            if (_rowPosition >= _rowCount)
                return false;

            int retval = mysql_stmt_fetch(_stmt);
            return retval == 0 || retval == MYSQL_DATA_TRUNCATED;
        }

        public ReadOnlySpan<Field> Fetch()
        {
            Assert(_rowPosition < _rowCount);
            return new ReadOnlySpan<Field>(&_rows[(int)_rowPosition * _fieldCount], _fieldCount);
        }

        void CleanUp()
        {
            if (_fieldMetadata != default)
            {
                Marshal.FreeHGlobal((IntPtr)_fieldMetadata);
                _fieldMetadata = default;
            }

            if (_metadataResult != default)
            {
                mysql_free_result(_metadataResult);
                _metadataResult = default;
            }

            if (_rBind != default)
            {
                Marshal.FreeHGlobal((IntPtr)_rBind->buffer);
                Marshal.FreeHGlobal((IntPtr)_rBind);
                _rBind = default;
            }

            if (_rows != default)
            {
                Marshal.FreeHGlobal((IntPtr)_rows);
                _rows = default;
            }
        }

        bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
            }

            CleanUp();

            _disposed = true;
        }
    }
}
