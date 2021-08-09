// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MySqlSharp;
using Common;

namespace Server.Database
{
    using static Log;
    using static Errors;
    using static NativeMethods;
    using static enum_field_types;
    using static enum_stmt_attr_type;

    public unsafe class MySqlPreparedStatement : IDisposable
    {
        IntPtr _Mstmt;
        int _paramCount;
        bool[] _paramsSet;
        MYSQL_BIND* _bind;
        string _queryString;

        protected PreparedStatementBase? _stmt;

        public IntPtr STMT => _Mstmt;
        public MYSQL_BIND* Bind => _bind;

        public int ParameterCount => _paramCount;

        public MySqlPreparedStatement(IntPtr stmt, string queryString)
        {
            _Mstmt = stmt;
            _queryString = queryString;
            _paramCount = mysql_stmt_param_count(stmt);
            _paramsSet = new bool[_paramCount];

            _bind = (MYSQL_BIND*)Marshal.AllocHGlobal(sizeof(MYSQL_BIND) * _paramCount);
            new Span<byte>(_bind, sizeof(MYSQL_BIND) * _paramCount).Fill(0);

            // "If set to true, causes mysql_stmt_store_result() to update the metadata MYSQL_FIELD->max_length value."
            mysql_stmt_attr_set(stmt, STMT_ATTR_UPDATE_MAX_LENGTH, true);
        }

        ~MySqlPreparedStatement()
        {
            Dispose(false);
        }

        public void BindParameters(PreparedStatementBase stmt)
        {
            _stmt = stmt;

            byte pos = 0;
            foreach (var data in stmt.Parameters)
            {
                if (pos == stmt.ParameterCount) break;

                switch (data)
                {
                    case bool val:
                        SetParameter(pos, val);
                        break;
                    case byte val:
                        SetParameter(pos, val);
                        break;
                    case sbyte val:
                        SetParameter(pos, val);
                        break;
                    case ushort val:
                        SetParameter(pos, val);
                        break;
                    case short val:
                        SetParameter(pos, val);
                        break;
                    case uint val:
                        SetParameter(pos, val);
                        break;
                    case int val:
                        SetParameter(pos, val);
                        break;
                    case ulong val:
                        SetParameter(pos, val);
                        break;
                    case long val:
                        SetParameter(pos, val);
                        break;
                    case float val:
                        SetParameter(pos, val);
                        break;
                    case double val:
                        SetParameter(pos, val);
                        break;
                    case decimal val:
                        SetParameter(pos, val);
                        break;
                    case null:
                        SetParameterNull(pos);
                        break;
                    case byte[] val:
                        SetParameter(pos, val);
                        break;
                    case string val:
                        SetParameter(pos, val);
                        break;
                    case DateTime val:
                        SetParameter(pos, val);
                        break;
                    case TimeSpan val:
                        SetParameter(pos, val);
                        break;
                    default:
                        FEL_LOG_WARN("sql.sql", "[WARN] Prepared Statement (id: {0}, param pos: {1}) bound to unsupported parameter data type: {2}!", _stmt!.Index, pos, data.GetType().Name);
                        break;
                }
                ++pos;
            }
        }

        static enum_field_types GetFieldType<T>() where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
                return MYSQL_TYPE_TINY;
            if (typeof(T) == typeof(sbyte))
                return MYSQL_TYPE_TINY;

            if (typeof(T) == typeof(ushort))
                return MYSQL_TYPE_SHORT;
            if (typeof(T) == typeof(short))
                return MYSQL_TYPE_SHORT;

            if (typeof(T) == typeof(uint))
                return MYSQL_TYPE_LONG;
            if (typeof(T) == typeof(int))
                return MYSQL_TYPE_LONG;

            if (typeof(T) == typeof(ulong))
                return MYSQL_TYPE_LONGLONG;
            if (typeof(T) == typeof(long))
                return MYSQL_TYPE_LONGLONG;

            if (typeof(T) == typeof(float))
                return MYSQL_TYPE_FLOAT;
            if (typeof(T) == typeof(double))
                return MYSQL_TYPE_DOUBLE;
            if (typeof(T) == typeof(decimal))
                return MYSQL_TYPE_DECIMAL;

            Assert(false, "Invalid type to field type: {0}", typeof(T).Name);
            return default;
        }

        static Dictionary<Type, bool> _unsignedTypes = new();

        static bool IsUnsigned<T>() where T : unmanaged
        {
            bool unsigned;
            if (!_unsignedTypes.TryGetValue(typeof(T), out unsigned))
            {
                var fieldInfo = typeof(T).GetField("MinValue");
                if (fieldInfo == null)
                    unsigned = _unsignedTypes[typeof(T)] = false;
                else
                    unsigned = _unsignedTypes[typeof(T)] = !Convert.ToBoolean(fieldInfo.GetValue(null));
            }

            return unsigned;
        }

        protected void SetParameterNull(byte index)
        {
            AssertValidIndex(index);
            _paramsSet[index] = true;

            MYSQL_BIND* bind = &_bind[index];

            bind->buffer_type = MYSQL_TYPE_NULL;
            Marshal.FreeHGlobal((IntPtr)bind->buffer);
            bind->buffer = default;
            bind->buffer_length = default;
            bind->is_null_value = true;
            Marshal.FreeHGlobal((IntPtr)bind->length);
            bind->length = default;
        }

        protected void SetParameter(byte index, bool value)
        {
            SetParameter(index, (byte)(value ? 1 : 0));
        }

        protected void SetParameter<T>(byte index, T value) where T : unmanaged
        {
            AssertValidIndex(index);
            _paramsSet[index] = true;

            MYSQL_BIND* bind = &_bind[index];

            var len = sizeof(T);
            bind->buffer_type = GetFieldType<T>();
            Marshal.FreeHGlobal((IntPtr)bind->buffer);

            bind->buffer = Marshal.AllocHGlobal(len).ToPointer();
            bind->buffer_length = default;
            bind->is_null_value = false;
            Marshal.FreeHGlobal((IntPtr)bind->length); // bind->length Only != nullptr for strings
            bind->is_unsigned = IsUnsigned<T>();

            MemoryMarshal.Write(new Span<byte>(bind->buffer, len), ref value);
        }

        protected void SetParameter(byte index, string value)
        {
            AssertValidIndex(index);
            _paramsSet[index] = true;

            var byteCount = new CULong((uint)Encoding.UTF8.GetByteCount(value));

            MYSQL_BIND* bind = &_bind[index];

            bind->buffer_type = MYSQL_TYPE_VAR_STRING;
            Marshal.FreeHGlobal((IntPtr)bind->buffer);
            bind->buffer = Marshal.AllocHGlobal((int)byteCount.Value).ToPointer();
            bind->buffer_length = byteCount;
            bind->is_null_value = false;

            Marshal.FreeHGlobal((IntPtr)bind->length);
            var lengthMem = Marshal.AllocHGlobal(sizeof(CULong)).ToPointer();
            MemoryMarshal.Write(new Span<byte>(lengthMem, sizeof(CULong)), ref byteCount);
            bind->length = (CULong*)lengthMem;

            Encoding.UTF8.GetBytes(value, new Span<byte>(bind->buffer, (int)byteCount.Value));
        }

        protected void SetParameter(byte index, DateTime value)
        {
            Assert(value.Kind != DateTimeKind.Local, "Prepared statement {0} cannot set a local DateTime value {1}", index, value);

            if (value <= Time.UnixEpoch)
                SetParameter(index, 0U);
            else
                SetParameter(index, (uint)Time.GetTimestamp(value));
        }

        protected void SetParameter(byte index, TimeSpan value)
        {
            Assert(value.Days <= 34, "Prepared statement {0} cannot set a TimeSpan value {1} that its days is longer than 34", index, value);
            SetParameter(index, value.ToString(@"d\ hh\:mm\:ss"));
        }

        protected void SetParameter(byte index, ReadOnlySpan<byte> value)
        {
            AssertValidIndex(index);
            _paramsSet[index] = true;

            MYSQL_BIND* bind = &_bind[index];

            var len = new CULong((uint)value.Length);
            bind->buffer_type = MYSQL_TYPE_BLOB;
            Marshal.FreeHGlobal((IntPtr)bind->buffer);
            bind->buffer = Marshal.AllocHGlobal((int)len.Value).ToPointer();
            bind->buffer_length = len;
            bind->is_null_value = false;

            Marshal.FreeHGlobal((IntPtr)bind->length);
            var lengthMem = Marshal.AllocHGlobal(sizeof(CULong)).ToPointer();
            MemoryMarshal.Write(new Span<byte>(lengthMem, sizeof(CULong)), ref len);
            bind->length = (CULong*)lengthMem;

            value.CopyTo(new Span<byte>(bind->buffer, (int)len.Value));
        }

        public void ClearParameters()
        {
            for (var i = 0; i < _paramCount; ++i)
            {
                Marshal.FreeHGlobal((IntPtr)(_bind[i].length));
                _bind[i].length = default;

                Marshal.FreeHGlobal((IntPtr)_bind[i].buffer);
                _bind[i].buffer = default;

                _paramsSet[i] = false;
            }
        }

        static bool ParamenterIndexAssertFail(int stmtIndex, int index, int paramCount)
        {
            FEL_LOG_ERROR("sql.driver", "Attempted to bind parameter {0}{1} on a PreparedStatement {2}" +
                " (statement has only {3} parameters)", index + 1,
                (index == 1 ? "st" : (index == 2 ? "nd" : (index == 3 ? "rd" : "nd"))), stmtIndex, paramCount);
            return false;
        }

        protected void AssertValidIndex(byte index)
        {
            Assert(index < _paramCount || ParamenterIndexAssertFail(_stmt!.Index, index, _paramCount));

            if (_paramsSet[index])
                FEL_LOG_ERROR("sql.sql", "[ERROR] Prepared Statement (id: {0}) trying to bind value on already bound index ({1}).", _stmt!.Index, index);
        }

        public string GetQueryString()
        {
            if (string.IsNullOrEmpty(_queryString))
                return string.Empty;

            var sb = new StringBuilder(_queryString);
            int startIndex = 0;
            foreach (var value in _stmt!.Parameters)
            {
                var index = sb.ToString().IndexOf('?', startIndex);
                if (index != -1)
                {
                    startIndex = index;

                    sb.Remove(index, 1);

                    string valueStr = string.Empty;

                    switch (value)
                    {
                        case bool val:
                            valueStr = val ? "1" : "0";
                            break;
                        case byte val:
                            valueStr = val.ToString();
                            break;
                        case sbyte val:
                            valueStr = val.ToString();
                            break;
                        case ushort val:
                            valueStr = val.ToString();
                            break;
                        case short val:
                            valueStr = val.ToString();
                            break;
                        case uint val:
                            valueStr = val.ToString();
                            break;
                        case int val:
                            valueStr = val.ToString();
                            break;
                        case ulong val:
                            valueStr = val.ToString();
                            break;
                        case long val:
                            valueStr = val.ToString();
                            break;
                        case float val:
                            valueStr = val.ToString();
                            break;
                        case double val:
                            valueStr = val.ToString();
                            break;
                        case decimal val:
                            valueStr = val.ToString();
                            break;
                        case null:
                            valueStr = "NULL";
                            break;
                        case byte[] val:
                            valueStr = "BINARY";
                            break;
                        case string val:
                            valueStr = $"'{val}'";
                            break;
                        case DateTime val:
                            valueStr = $"'{val.ToString("yyyy-MM-dd HH:mm:ss")}'";
                            break;
                    }

                    sb.Insert(index, valueStr);
                }
            }

            return sb.ToString();
        }

        bool _disposed;

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

                ClearParameters();

                if (DatabaseLoader.IsMySQL8)
                {
                    if (((MYSQL_STMT*)_Mstmt)->bind_result_done != 0)
                    {
                        Marshal.FreeHGlobal((IntPtr)((MYSQL_STMT*)_Mstmt)->bind->length);
                        Marshal.FreeHGlobal((IntPtr)((MYSQL_STMT*)_Mstmt)->bind->is_null);
                    }
                }
                else
                {
                    if (((MYSQL_STMT_OLD*)_Mstmt)->bind_result_done != 0)
                    {
                        Marshal.FreeHGlobal((IntPtr)((MYSQL_STMT_OLD*)_Mstmt)->bind->length);
                        Marshal.FreeHGlobal((IntPtr)((MYSQL_STMT_OLD*)_Mstmt)->bind->is_null);
                    }
                }

                mysql_stmt_close(_Mstmt);

                Marshal.FreeHGlobal((IntPtr)_bind);
                _bind = default;
            }
            _disposed = true;
        }


    }
}
