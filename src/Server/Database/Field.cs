// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using MySqlSharp;
using static Common.Errors;
using static Common.Log;

namespace Server.Database
{
    public enum DatabaseFieldTypes : byte
    {
        Null,
        Int8,
        Int16,
        Int32,
        Int64,
        Float,
        Double,
        Decimal,
        Date,
        Binary
    }

    public struct QueryResultFieldMetadata
    {
        public IntPtr TableName;
        public IntPtr TableAlias;
        public IntPtr Name;
        public IntPtr Alias;
        public enum_field_types FieldType;
        public int Index;
        public DatabaseFieldTypes Type;
    }

    public unsafe struct FieldData
    {
        public byte* Value;                 // Actual data in memory
        public int Length;                  // Length
        public bool Raw;                    // Raw bytes? (Prepared statement or ad hoc)
        public ReadOnlySpan<byte> Span => new ReadOnlySpan<byte>(Value, Length);
    }

    /**
        Class used to access individual fields of database query result

        Guideline on field type matching:

        |   MySQL type           |  method to use                         |
        |------------------------|----------------------------------------|
        | TINYINT                | GetBool, GetInt8, GetUInt8             |
        | SMALLINT               | GetInt16, GetUInt16                    |
        | MEDIUMINT, INT         | GetInt32, GetUInt32                    |
        | BIGINT                 | GetInt64, GetUInt64                    |
        | FLOAT                  | GetFloat                               |
        | DOUBLE, DECIMAL        | GetDouble                              |
        | CHAR, VARCHAR,         | GetString                  |
        | TINYTEXT, MEDIUMTEXT,  | GetString                  |
        | TEXT, LONGTEXT         | GetString                  |
        | TINYBLOB, MEDIUMBLOB,  | GetBinary, GetString                   |
        | BLOB, LONGBLOB         | GetBinary, GetString                   |
        | BINARY, VARBINARY      | GetBinary                              |

        Return types of aggregate functions:

        | Function |       Type        |
        |----------|-------------------|
        | MIN, MAX | Same as the field |
        | SUM, AVG | DECIMAL           |
        | COUNT    | BIGINT            |
    */
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Field
    {
        FieldData _data;

        QueryResultFieldMetadata* _meta;

        public bool GetBool() // Wrapper, actually gets integer
        {
            return GetUInt8() == 1 ? true : false;
        }

        public byte GetUInt8()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<byte>(_data.Span);

            byte.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public sbyte GetInt8()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<sbyte>(_data.Span);

            sbyte.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public ushort GetUInt16()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<ushort>(_data.Span);

            ushort.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public short GetInt16()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<short>(_data.Span);

            short.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public uint GetUInt32()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<uint>(_data.Span);

            uint.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public int GetInt32()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<int>(_data.Span);

            int.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public ulong GetUInt64()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<ulong>(_data.Span);

            ulong.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public long GetInt64()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<long>(_data.Span);

            long.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public float GetFloat()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<float>(_data.Span);

            float.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public double GetDouble()
        {
            if (_data.Value == default) return 0;

            if (_data.Raw)
                return MemoryMarshal.Read<double>(_data.Span);

            double.TryParse(Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length), out var ret);
            return ret;
        }

        public string GetString()
        {
            if (_data.Value == default) return string.Empty;

            return Marshal.PtrToStringAnsi((IntPtr)_data.Value, _data.Length);
        }

        public DateTime GetDateTime()
        {
            if (_data.Value == default)
                return DateTime.MinValue;

            Assert(_meta->Type == DatabaseFieldTypes.Int64, "Cannot get DateTime of field with data type: {0}, field data must be a unix timestamp!", _meta->FieldType);

            return DateTimeOffset.FromUnixTimeSeconds(GetInt64()).LocalDateTime;
        }

        public TimeSpan GetTimeSpan()
        {
            if (_data.Value == default)
                return TimeSpan.MinValue;

            Assert(IsNumeric(), "Cannot get TimeSpan of field with data type: {0}, field data must be a number of total seconds!", _meta->FieldType);

            return new TimeSpan(0, 0, (int)GetUInt32());
        }

        public int GetBinaryLength()
        {
            if (_data.Value == default) return 0;

            return _data.Length;
        }

        public byte[] GetBinary()
        {
            if (_data.Value == default || _data.Length == 0)
                return Array.Empty<byte>();

            var ret = new byte[_data.Length];
            _data.Span.CopyTo(ret);
            return ret;
        }

        public void GetBinary(Span<byte> destination)
        {
            if (_data.Value == default || _data.Length == 0)
                return;

            _data.Span.CopyTo(destination);
        }

        public byte[] GetBinary(int length)
        {
            Assert(_data.Value != default && (_data.Length == length), "Expected {0}-byte binary blob, "+
                "got {1}data ({2} bytes) instead", length, _data.Value != default ? "" : "no ", _data.Length);

            var ret = new byte[_data.Length];
            _data.Span.CopyTo(ret);
            return ret;
        }

        public void GetBinary(Span<byte> destination, int length)
        {
            Assert(_data.Value != default && (_data.Length == length), "Expected {0}-byte binary blob, "+
                "got {1}data ({2} bytes) instead", length, _data.Value != default ? "" : "no ", _data.Length);

            _data.Span.CopyTo(destination);
        }

        public bool IsNull()
        {
            return _data.Value == default;
        }

        public void SetByteValue(byte* newValue, int length)
        {
            // This value stores raw bytes that have to be explicitly cast later
            _data.Value = newValue;
            _data.Length = length;
            _data.Raw = true;
        }

        public void SetStructuredValue(byte* newValue, int length)
        {
            // This value stores somewhat structured data that needs function style casting
            _data.Value = newValue;
            _data.Length = length;
            _data.Raw = false;
        }

        public bool IsType(DatabaseFieldTypes type)
        {
            return _meta->Type == type;
        }

        public bool IsNumeric()
        {
            return (_meta->Type == DatabaseFieldTypes.Int8 ||
                _meta->Type == DatabaseFieldTypes.Int16 ||
                _meta->Type == DatabaseFieldTypes.Int32 ||
                _meta->Type == DatabaseFieldTypes.Int64 ||
                _meta->Type == DatabaseFieldTypes.Float ||
                _meta->Type == DatabaseFieldTypes.Double);
        }

        void LogWrongType(string getter)
        {
            FEL_LOG_WARN("sql.sql", "Warning: {0} on {1} field {2}.{3} ({4}.{5}) at index {6}.",
                getter,
                _meta->FieldType,
                Marshal.PtrToStringAnsi(_meta->TableAlias),
                Marshal.PtrToStringAnsi(_meta->Alias),
                Marshal.PtrToStringAnsi(_meta->TableName),
                Marshal.PtrToStringAnsi(_meta->Name), _meta->Index);
        }

        public void SetMetadata(QueryResultFieldMetadata* fieldMeta)
        {
            _meta = fieldMeta;
        }
    }
}
