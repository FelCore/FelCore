// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;
using System.Collections.Generic;
using MySqlSharp;
using Common;
using Common.Extensions;
using static Common.Log;
using static Common.Errors;
using static Server.Database.SqlElementDataType;
using static MySqlSharp.NativeMethods;
using static MySqlSharp.ErrorClient;
using static MySqlSharp.ErrorServer;
using static MySqlSharp.mysql_option;
using static MySqlSharp.mysql_protocol_type;
using static MySqlSharp.mysql_ssl_mode;
using static Server.Database.ConnectionFlags;
using static Common.Time;

namespace Server.Database
{
    public enum ConnectionFlags
    {
        CONNECTION_ASYNC = 0x1,
        CONNECTION_SYNCH = 0x2,
        CONNECTION_BOTH = CONNECTION_ASYNC | CONNECTION_SYNCH
    }

    public class MySqlConnectionInfo
    {
        public MySqlConnectionInfo(string infoString)
        {
            var tokens = infoString.Split(';');

            if (tokens.Length != 5 && tokens.Length != 6)
                return;

            Host = tokens[0];
            PortOrSocket = tokens[1];
            User = tokens[2];
            Password = tokens[3];
            Database = tokens[4];

            if (tokens.Length == 6)
                Ssl = tokens[5];
        }

        public string Host = "";
        public string PortOrSocket = "";
        public string User = "";
        public string Password = "";
        public string Database = "";
        public string Ssl = "";
    }

    public unsafe class MySqlConnection : IDisposable
    {
        IntPtr _mysql = IntPtr.Zero;

        protected bool _reconnecting; //! Are we reconnecting?
        protected bool _prepareError; //! Was there any error while preparing statements?

        protected MySqlPreparedStatement?[] _stmts = Array.Empty<MySqlPreparedStatement?>();
        public ReadOnlySpan<MySqlPreparedStatement?> Stmts => _stmts;

        ProducerConsumerQueue<SqlOperation>? _queue = null;
        DatabaseWorker? _worker;
        MySqlConnectionInfo _connectionInfo; //! Connection info (used for logging)
        ConnectionFlags _connectionFlags; //! Connection flags (for preparing relevant statements)
        object _mutex = new object();

        bool _disposed;
        public bool Disposed => _disposed;

        public MySqlConnection(MySqlConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
            _connectionFlags = ConnectionFlags.CONNECTION_SYNCH;
        }

        //! Constructor for asynchronous connections.
        public MySqlConnection(ProducerConsumerQueue<SqlOperation> queue, MySqlConnectionInfo connectionInfo)
        {
            _queue = queue;
            _connectionInfo = connectionInfo;
            _connectionFlags = ConnectionFlags.CONNECTION_ASYNC;
            _worker = new DatabaseWorker(_queue, this);
        }

        ~MySqlConnection()
        {
            Dispose(false);
        }

        public void Close()
        {
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
            }

            foreach (var stmt in _stmts)
                stmt?.Dispose();
            Array.Clear(_stmts, 0, _stmts.Length);

            if (_mysql != IntPtr.Zero)
            {
                mysql_close(_mysql);
                _mysql = IntPtr.Zero;
            }
        }

        public virtual int Open()
        {
            IntPtr mysqlInit = mysql_init();

            if (mysqlInit == IntPtr.Zero)
            {
                FEL_LOG_ERROR("sql.sql", "Could not initialize Mysql connection to database `{0}`", _connectionInfo.Database);
                return (int)CR_UNKNOWN_ERROR;
            }

            uint port = 0;
            string unix_socket;

            mysql_options(mysqlInit, (int)MYSQL_SET_CHARSET_NAME, "utf8");

            var option = DatabaseLoader.IsMySQL8 ? (int)MYSQL_OPT_PROTOCOL : (int)mysql_option_old.MYSQL_OPT_PROTOCOL;

            if (OperatingSystem.IsWindows())
            {
                if (_connectionInfo.Host == ".") // named pipe use option (Windows)
                {
                    mysql_options(mysqlInit, option, (int)MYSQL_PROTOCOL_PIPE);
                    port = 0;
                    unix_socket = string.Empty;
                }
                else
                {
                    uint.TryParse(_connectionInfo.PortOrSocket, out port);
                    unix_socket = string.Empty;
                }
            }
            else
            {
                if (_connectionInfo.Host == ".") // socket use option (Unix/Linux)
                {
                    mysql_options(mysqlInit, option, (int)MYSQL_PROTOCOL_SOCKET);
                    _connectionInfo.Host = "localhost";
                    port = 0;
                    unix_socket = _connectionInfo.PortOrSocket;
                }
                else
                {
                    uint.TryParse(_connectionInfo.PortOrSocket, out port);
                    unix_socket = string.Empty;
                }
            }


            if (_connectionInfo.Ssl != "")
            {
                if (mysql_get_client_version() >= 80000)
                {
                    mysql_ssl_mode opt_use_ssl = SSL_MODE_DISABLED;
                    if (_connectionInfo.Ssl == "ssl")
                        opt_use_ssl = SSL_MODE_REQUIRED;

                    option = DatabaseLoader.IsMySQL8 ? (int)MYSQL_OPT_SSL_MODE : (int)mysql_option_old.MYSQL_OPT_SSL_MODE;
                    mysql_options(mysqlInit, option, (int)opt_use_ssl);
                }
                else
                {
                    bool opt_use_ssl = false;
                    if (_connectionInfo.Ssl == "ssl")
                        opt_use_ssl = true;

                    const int MYSQL_OPT_SSL_ENFORCE = 38; // Exists in MySQL 5.7 but not 8.0+
                    option = DatabaseLoader.IsMySQL8 ? (int)MYSQL_OPT_SSL_ENFORCE : (int)mysql_option_old.MYSQL_OPT_SSL_ENFORCE;
                    mysql_options(mysqlInit, option, opt_use_ssl);
                }
            }

            _mysql = mysql_real_connect(mysqlInit, _connectionInfo.Host, _connectionInfo.User,
                _connectionInfo.Password, _connectionInfo.Database, port, unix_socket);

            if (_mysql != IntPtr.Zero)
            {
                if (!_reconnecting)
                {
                    FEL_LOG_INFO("sql.sql", "MySQL client library: {0}", mysql_get_client_info());
                    FEL_LOG_INFO("sql.sql", "MySQL server ver: {0} ", mysql_get_server_info(_mysql));
                    // MySQL version above 5.1 IS required in both client and server and there is no known issue with different versions above 5.1
                    // if (mysql_get_server_version(_mysqlHandle) != mysql_get_client_version())
                    //     FEL_LOG_INFO("sql.sql", "[WARNING] MySQL client/server version mismatch; may conflict with behaviour of prepared statements.");
                }

                FEL_LOG_INFO("sql.sql", "Connected to MySQL database at {0}", _connectionInfo.Host);
                mysql_autocommit(_mysql, true);

                // set connection properties to UTF8 to properly handle locales for different
                // server configs - core sends data in UTF8, so MySQL must expect UTF8 too
                mysql_set_character_set(_mysql, "utf8");
                return 0;
            }
            else
            {
                FEL_LOG_ERROR("sql.sql", "Could not connect to MySQL database at {0}: {1}", _connectionInfo.Host, mysql_error(mysqlInit));
                var errorCode = mysql_errno(mysqlInit);
                mysql_close(mysqlInit);
                return errorCode;
            }
        }

        public bool PrepareStatements()
        {
            DoPrepareStatements();
            return !_prepareError;
        }

        protected virtual void DoPrepareStatements() { }

        protected MySqlPreparedStatement? GetPreparedStatement(int index)
        {
            Assert(index < _stmts.Length, "Tried to access invalid prepared statement index {0} (max index {1}) on database `{2}`, connection type: {3}",
                index, _stmts.Length, _connectionInfo.Database, (_connectionFlags & CONNECTION_ASYNC) != 0 ? "asynchronous" : "synchronous");

            var ret = _stmts[index];
            if (ret == null)
            {
                FEL_LOG_ERROR("sql.sql", "Could not fetch prepared statement {0} on database `{1}`, connection type: {2}.",
                    index, _connectionInfo.Database, (_connectionFlags & CONNECTION_ASYNC) != 0 ? "asynchronous" : "synchronous");
            }

            return ret;
        }

        protected void PrepareStatement(int index, string sql, ConnectionFlags flags)
        {
            // Check if specified query should be prepared on this connection
            // i.e. don't prepare async statements on synchronous connections
            // to save memory that will not be used.
            if ((_connectionFlags & flags) == 0)
            {
                _stmts[index] = null;
                return;
            }

            var stmt = mysql_stmt_init(_mysql);
            if (stmt == IntPtr.Zero)
            {
                FEL_LOG_ERROR("sql.sql", "In mysql_stmt_init() id: {0}, sql: \"{1}\"", index, sql);
                FEL_LOG_ERROR("sql.sql", "{0}", mysql_error(_mysql));
                _prepareError = true;
            }
            else
            {
                if (mysql_stmt_prepare(stmt, sql) != 0)
                {
                    FEL_LOG_ERROR("sql.sql", "In mysql_stmt_prepare() id: {0}, sql: \"{1}\"", index, sql);
                    FEL_LOG_ERROR("sql.sql", "{0}", mysql_stmt_error(stmt));
                    mysql_stmt_close(stmt);
                    _prepareError = true;
                }
                else
                    _stmts[index] = new MySqlPreparedStatement(stmt, sql);
            }
        }

        public bool Execute(string sql)
        {
            if (_mysql == IntPtr.Zero) return false;

            var _s = GetMSTime();

            if (mysql_query(_mysql, sql) != 0)
            {
                var lErrno = mysql_errno(_mysql);

                FEL_LOG_INFO("sql.sql", "SQL: {0}", sql);
                FEL_LOG_ERROR("sql.sql", "[{0}] {1}", lErrno, mysql_error(_mysql));

                if (_HandleMySqlErrno(lErrno))  // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Execute(sql);        // Try again

                return false;
            }
            else
                FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL: {1}", GetMSTimeDiff(_s, GetMSTime()), sql);

            return true;
        }

        public bool Execute(PreparedStatementBase stmt)
        {
            if (_mysql == IntPtr.Zero) return false;

            int index = stmt.Index;
            var mStmt = GetPreparedStatement(index);
            Assert(mStmt != null); // Can only be null if preparation failed, server side error or bad query

            mStmt!.BindParameters(stmt);

            var msql_STMT = mStmt.STMT;
            var msql_BIND = mStmt.Bind;

            var _s = GetMSTime();

            if (mysql_stmt_bind_param(msql_STMT, msql_BIND))
            {
                var lErrno = mysql_errno(_mysql);
                FEL_LOG_ERROR("sql.sql", "SQL(p): {0}\n [ERROR]: [{1}] {2}", mStmt.GetQueryString(), lErrno, mysql_stmt_error(msql_STMT));

                if (_HandleMySqlErrno(lErrno))  // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Execute(stmt);       // Try again

                mStmt.ClearParameters();
                return false;
            }

            if (mysql_stmt_execute(msql_STMT) != 0)
            {
                var lErrno = mysql_errno(_mysql);
                FEL_LOG_ERROR("sql.sql", "SQL(p): {0}\n [ERROR]: [{1}] {2}", mStmt.GetQueryString(), lErrno, mysql_stmt_error(msql_STMT));

                if (_HandleMySqlErrno(lErrno))  // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Execute(stmt);       // Try again

                mStmt.ClearParameters();
                return false;
            }

            FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL(p): {1}", GetMSTimeDiff(_s, GetMSTime()), mStmt.GetQueryString());

            mStmt.ClearParameters();
            return true;
        }

        bool _Query(string sql, ref IntPtr pResult, ref MYSQL_FIELD* pFields, ref long pRowCount, ref int pFieldCount)
        {
            if (_mysql == IntPtr.Zero)
                return false;

            {
                var _s = GetMSTime();

                if (mysql_query(_mysql, sql) != 0)
                {
                    var lErrno = mysql_errno(_mysql);
                    FEL_LOG_INFO("sql.sql", "SQL: {0}", sql);
                    FEL_LOG_ERROR("sql.sql", "[{0}] {1}", lErrno, mysql_error(_mysql));

                    if (_HandleMySqlErrno(lErrno))      // If it returns true, an error was handled successfully (i.e. reconnection)
                        return _Query(sql, ref pResult, ref pFields, ref pRowCount, ref pFieldCount);    // We try again

                    return false;
                }
                else
                    FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL: {1}", GetMSTimeDiff(_s, GetMSTime()), sql);

                pResult = mysql_store_result(_mysql);
                pRowCount = mysql_affected_rows(_mysql);
                pFieldCount = mysql_field_count(_mysql);
            }

            if ((IntPtr)pResult == IntPtr.Zero)
                return false;

            if (pRowCount == 0)
            {
                mysql_free_result(pResult);
                return false;
            }

            pFields = mysql_fetch_fields(pResult);

            return true;
        }

        public QueryResult? Query(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return null;

            IntPtr result = default;
            MYSQL_FIELD* fields = default;
            long rowCount = 0;
            int fieldCount = 0;

            if (!_Query(sql, ref result, ref fields, ref rowCount, ref fieldCount))
                return null;

            return new QueryResult(result, fields, rowCount, fieldCount);
        }

        bool _Query(PreparedStatementBase stmt, ref MySqlPreparedStatement? mysqlStmt, ref IntPtr pResult, ref long pRowCount, ref int pFieldCount)
        {
            if (_mysql == default)
                return false;

            var index = stmt.Index;

            var mStmt = GetPreparedStatement(index);
            Assert(mStmt != null);            // Can only be null if preparation failed, server side error or bad query

            mStmt!.BindParameters(stmt);
            mysqlStmt = mStmt;

            var msql_STMT = mStmt.STMT;
            MYSQL_BIND* msql_BIND = mStmt.Bind;

            var _s = GetMSTime();

            if (mysql_stmt_bind_param(msql_STMT, msql_BIND))
            {
                var lErrno = mysql_errno(_mysql);
                FEL_LOG_ERROR("sql.sql", "SQL(p): {0}\n [ERROR]: [{1}] {2}", mStmt.GetQueryString(), lErrno, mysql_stmt_error(msql_STMT));

                if (_HandleMySqlErrno(lErrno))  // If it returns true, an error was handled successfully (i.e. reconnection)
                    return _Query(stmt, ref mysqlStmt, ref pResult, ref pRowCount, ref pFieldCount);       // Try again

                mStmt.ClearParameters();
                return false;
            }

            if (mysql_stmt_execute(msql_STMT) != 0)
            {
                var lErrno = mysql_errno(_mysql);
                FEL_LOG_ERROR("sql.sql", "SQL(p): {0}\n [ERROR]: [{1}] {2}", mStmt.GetQueryString(), lErrno, mysql_stmt_error(msql_STMT));

                if (_HandleMySqlErrno(lErrno))  // If it returns true, an error was handled successfully (i.e. reconnection)
                    return _Query(stmt, ref mysqlStmt, ref pResult, ref pRowCount, ref pFieldCount);      // Try again

                mStmt.ClearParameters();
                return false;
            }

            FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL(p): {1}", GetMSTimeDiff(_s, GetMSTime()), mStmt.GetQueryString());

            mStmt.ClearParameters();

            pResult = mysql_stmt_result_metadata(msql_STMT);
            pRowCount = mysql_stmt_num_rows(msql_STMT);
            pFieldCount = mysql_stmt_field_count(msql_STMT);

            return true;
        }

        public PreparedQueryResult? Query(PreparedStatementBase stmt)
        {
            MySqlPreparedStatement? mysqlStmt = null;
            IntPtr result = default;
            long rowCount = 0;
            int fieldCount = 0;

            if (!_Query(stmt, ref mysqlStmt, ref result, ref rowCount, ref fieldCount))
                return null;

            if (mysql_more_results(_mysql))
            {
                mysql_next_result(_mysql);
            }
            return new PreparedQueryResult(mysqlStmt!.STMT, result, rowCount, fieldCount);
        }

        public void BeginTransaction()
        {
            Execute("START TRANSACTION");
        }

        public void RollbackTransaction()
        {
            Execute("ROLLBACK");
        }

        public void CommitTransaction()
        {
            Execute("COMMIT");
        }

        public int ExecuteTransaction(SqlTransactionBase transaction)
        {
            if (transaction.Queries.Count == 0)
                return -1;

            BeginTransaction();

            foreach(var query in transaction.Queries)
            {
                switch (query.Type)
                {
                    case SQL_ELEMENT_PREPARED:
                    {
                        var stmt = query.Element.Stmt;

                        if (!Execute(stmt!))
                        {
                            FEL_LOG_WARN("sql.sql", "Transaction aborted. {0} queries not executed.", transaction.Queries.Count);
                            var errorCode = GetLastError();
                            RollbackTransaction();
                            return errorCode;
                        }
                        break;
                    }
                    case SQL_ELEMENT_RAW:
                    {
                        var sql = query.Element.Query;
                        Assert(!string.IsNullOrEmpty(sql));
                        if (!Execute(sql!))
                        {
                            FEL_LOG_WARN("sql.sql", "Transaction aborted. {0} queries not executed.", transaction.Queries.Count);
                            var errorCode = GetLastError();
                            RollbackTransaction();
                            return errorCode;
                        }
                        break;
                    }
                }
            }

            // we might encounter errors during certain queries, and depending on the kind of error
            // we might want to restart the transaction. So to prevent data loss, we only clean up when it's all done.
            // This is done in calling functions DatabaseWorkerPool<T>::DirectCommitTransaction and TransactionTask::Execute,
            // and not while iterating over every element.

            CommitTransaction();
            return 0;
        }

        public int EscapeString(ref string str)
        {
            return mysql_real_escape_string(_mysql, ref str);
        }

        public void Ping()
        {
            mysql_ping(_mysql);
        }

        public int GetLastError()
        {
            return mysql_errno(_mysql);
        }

        public bool LockIfReady()
        {
            return Monitor.TryEnter(_mutex);
        }

        public void Unlock()
        {
            Monitor.Exit(_mutex);
        }

        public int GetServerVersion()
        {
            return mysql_get_server_version(_mysql);
        }

        bool _HandleMySqlErrno(int errNo, byte attempts = 5)
        {
            switch (errNo)
            {
                case (int)CR_SERVER_GONE_ERROR:
                case (int)CR_SERVER_LOST:
                case (int)CR_SERVER_LOST_EXTENDED:
                {
                    if (_mysql != IntPtr.Zero)
                    {
                        FEL_LOG_ERROR("sql.sql", "Lost the connection to the MySQL server!");

                        mysql_close(_mysql);
                        _mysql = default;
                    }
                    goto case (int)CR_CONN_HOST_ERROR;
                }
                case (int)CR_CONN_HOST_ERROR:
                {
                    FEL_LOG_INFO("sql.sql", "Attempting to reconnect to the MySQL server...");

                    _reconnecting = true;

                    var lErrno = Open();
                    if (lErrno == 0)
                    {
                        // Don't remove 'this' pointer unless you want to skip loading all prepared statements...
                        if (!this.PrepareStatements())
                        {
                            FEL_LOG_FATAL("sql.sql", "Could not re-prepare statements!");
                            Thread.Sleep(10000);
                            Environment.FailFast(null);
                        }

                        FEL_LOG_INFO("sql.sql", "Successfully reconnected to {0} @{1}:{2} ({3}).",
                            _connectionInfo.Database, _connectionInfo.Host, _connectionInfo.PortOrSocket,
                                (_connectionFlags & CONNECTION_ASYNC) != 0 ? "asynchronous" : "synchronous");

                        _reconnecting = false;
                        return true;
                    }

                    if ((--attempts) == 0)
                    {
                        // Shut down the server when the mysql server isn't
                        // reachable for some time
                        FEL_LOG_FATAL("sql.sql", "Failed to reconnect to the MySQL server, " +
                                    "terminating the server to prevent data corruption!");

                        // We could also initiate a shutdown through using std::raise(SIGTERM)
                        Thread.Sleep(10000);
                        Environment.FailFast(null);
                        return false;
                    }
                    else
                    {
                        // It's possible this attempted reconnect throws 2006 at us.
                        // To prevent crazy recursive calls, sleep here.
                        Thread.Sleep(3000); // Sleep 3 seconds
                        return _HandleMySqlErrno(lErrno, attempts); // Call self (recursive)
                    }
                }

                case (int)ER_LOCK_DEADLOCK:
                    return false;    // Implemented in TransactionTask::Execute and DatabaseWorkerPool<T>::DirectCommitTransaction
                // Query related errors - skip query
                case (int)ER_WRONG_VALUE_COUNT:
                case (int)ER_DUP_ENTRY:
                    return false;

                // Outdated table or database structure - terminate core
                case (int)ER_BAD_FIELD_ERROR:
                case (int)ER_NO_SUCH_TABLE:
                    FEL_LOG_ERROR("sql.sql", "Your database structure is not up to date. Please make sure you've executed all queries in the sql/updates folders.");
                    Thread.Sleep(10000);
                    Environment.FailFast(null);
                    return false;
                case (int)ER_PARSE_ERROR:
                    FEL_LOG_ERROR("sql.sql", "Error while parsing SQL. Core fix required.");
                    Thread.Sleep(10000);
                    Environment.FailFast(null);
                    return false;
                default:
                    FEL_LOG_ERROR("sql.sql", "Unhandled MySQL errno {0}. Unexpected behaviour possible.", errNo);
                    return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Close();
            }

            if (_mysql != IntPtr.Zero)
            {
                mysql_close(_mysql);
                _mysql = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    public class MySqlConnection<Statements> : MySqlConnection where Statements : unmanaged, Enum
    {
        public MySqlConnection(MySqlConnectionInfo connectionInfo) : base(connectionInfo)
        {
        }

        //! Constructor for asynchronous connections.
        public MySqlConnection(ProducerConsumerQueue<SqlOperation> queue, MySqlConnectionInfo connectionInfo) : base(queue, connectionInfo)
        {
        }

        public MySqlPreparedStatement? GetPreparedStatement(Statements index)
        {
            return GetPreparedStatement(index.AsInteger<Statements, int>());
        }

        protected void PrepareStatement(Statements index, string sql, ConnectionFlags flags)
        {
            PrepareStatement(index.AsInteger<Statements, int>(), sql, flags);
        }
    }
}
