// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using Common;
using static Common.Log;
using static Common.Errors;

namespace Server.Database
{
    //- Union that holds element data
    [StructLayout(LayoutKind.Explicit)]
    public struct SqlElementUnion
    {
        [FieldOffset(0)]
        public PreparedStatementBase? Stmt;

        [FieldOffset(0)]
        public string? Query;
    }

    //- Type specifier of our element data
    public enum SqlElementDataType
    {
        SQL_ELEMENT_RAW,
        SQL_ELEMENT_PREPARED
    };

    //- The element
    public struct SqlElementData
    {
        public SqlElementUnion Element;
        public SqlElementDataType Type;
    };

    public class SqlOperation
    {
        public SqlOperation()
        {
            _conn = null;
        }

        public virtual int Call()
        {
            Execute();
            return 0;
        }
        public virtual bool Execute() { return false; }
        public virtual void SetConnection(MySqlConnection conn) { _conn = conn; }

        private MySqlConnection? _conn;
        public MySqlConnection? Conn => _conn;
    }
}
