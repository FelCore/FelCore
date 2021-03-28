// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;
using MySqlConnector;
using MySqlConnector.Core;
using Common;
using Common.Extensions;
using static Common.Log;
using static Common.Errors;
using static Server.Database.SqlElementDataType;

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
            Port = tokens[1];
            User = tokens[2];
            Password = tokens[3];
            Database = tokens[4];

            if (tokens.Length == 6)
                Ssl = tokens[5];
        }

        public string Host = "";
        public string Port = "";
        public string User = "";
        public string Password = "";
        public string Database = "";
        public string Ssl = "";

        public string GetConnectionString()
        {
            return $"Server={Host};Port={Port};User Id={User};Password={Password};Database={Database};Allow User Variables=True;Pooling=false;Character Set=utf8;";
        }
    }

    public class MySqlConnectionProxyBase : IDisposable
    {
        MySqlConnection? _connection;
        public MySqlConnection? MySqlConnection => _connection;

        protected static (string?, int)[] _preparedQueries = new (string?, int)[0];
        public static (string?, int)[] PreparedQueries => _preparedQueries;

        protected bool _reconnecting; //! Are we reconnecting?
        protected bool _prepareError; //! Was there any error while preparing statements?

        ProducerConsumerQueue<SqlOperation>? _queue = null;
        DatabaseWorker? _worker;
        MySqlConnectionInfo _connectionInfo; //! Connection info (used for logging)
        string _connectionString;
        ConnectionFlags _connectionFlags; //! Connection flags (for preparing relevant statements)
        object _mutex = new object();

        MySqlErrorCode _lastError;

        bool _disposed;
        public bool Disposed => _disposed;

        public MySqlConnectionProxyBase(MySqlConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
            _connectionString = connectionInfo.GetConnectionString();

            _connectionFlags = ConnectionFlags.CONNECTION_SYNCH;
        }

        //! Constructor for asynchronous connections.
        public MySqlConnectionProxyBase(ProducerConsumerQueue<SqlOperation> queue, MySqlConnectionInfo connectionInfo)
        {
            _queue = queue;

            _connectionInfo = connectionInfo;
            _connectionString = connectionInfo.GetConnectionString();

            _connectionFlags = ConnectionFlags.CONNECTION_ASYNC;
            _worker = new DatabaseWorker(_queue, this);
        }

        public void Close()
        {
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
            }

            if (_connection != null)
            {
                _connection.Close();
                _connection = null;
            }
        }

        public virtual MySqlErrorCode Open()
        {
            try
            {
                if (_connection != null)
                    _connection.Close();

                _connection = new MySqlConnection(_connectionString);
                _connection.Open();

                if (!_reconnecting)
                {
                    FEL_LOG_INFO("sql.sql", "MySQL server ver: {0} ", _connection.ServerVersion);
                }

                FEL_LOG_INFO("sql.sql", "Connected to MySQL database at {0}", _connectionInfo.Host);

                //TODO:
                //mysql_autocommit(m_Mysql, 1);

                //// set connection properties to UTF8 to properly handle locales for different
                //// server configs - core sends data in UTF8, so MySQL must expect UTF8 too
                //mysql_set_character_set(m_Mysql, "utf8");

                return MySqlErrorCode.None;
            }
            catch (MySqlException ex)
            {
                FEL_LOG_ERROR("sql.sql", "Could not connect to MySQL database at {0}: {1}", _connectionInfo.Host, ex.Message);
                return ex.ErrorCode;
            }
        }

        public bool PrepareStatements()
        {
            DoPrepareStatements();
            return !_prepareError;
        }

        protected virtual void DoPrepareStatements() { }

        protected PreparedStatement GetPreparedStatement(int index)
        {
            var sql = _preparedQueries[index].Item1;
            if (sql == null)
            {
                Assert(false, string.Format("Prepared statement {0} is null in database {1}", index, GetType().Name));
                throw new Exception();
            }
            return new PreparedStatement(sql, _preparedQueries[index].Item2);
        }

        protected void PrepareStatement(int index, string sql, ConnectionFlags flags)
        {
            if (_connection == null)
            {
                Assert(false, "MySQL Connection must be open!");
                return;
            }

            // Check if specified query should be prepared on this connection
            // i.e. don't prepare async statements on synchronous connections
            // to save memory that will not be used.
            if ((_connectionFlags & flags) == 0)
                return;

            if (_preparedQueries[index].Item1 != null) // Already prepared
                return;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Prepare();

                    var stmt = ((IMySqlCommand)cmd).TryGetPreparedStatements();
                    if (stmt == null)
                    {
                        _prepareError = true;
                        FEL_LOG_ERROR("sql.sql", "Prepare statement failed, id: {0}, sql: \"{1}\", error: {2}", index, sql, "No prepared statement found");
                        return;
                    }

                    var parameters = stmt.Statements[0].Parameters;
                    _preparedQueries[index] = (sql, parameters == null ? 0 : parameters.Length);
                }
            }
            catch (Exception ex)
            {
                _prepareError = true;
                FEL_LOG_ERROR("sql.sql", "Prepare statement failed, id: {0}, sql: \"{1}\", error: {2}", index, sql, ex.Message);
                return;
            }
        }

        public bool Execute(string sql)
        {
            if (_connection == null) return false;

            try
            {
                var now = Time.Now;

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();

                    FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL: {1}", (Time.Now - now).TotalMilliseconds, sql);

                    return true;
                }
            }
            catch (MySqlException ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", sql);
                FEL_LOG_ERROR("sql.sql", "[{0}] {1}", ex.ErrorCode, ex.Message);

                if (HandleMySqlException(ex.ErrorCode)) // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Execute(sql); // Try again

                _lastError = ex.ErrorCode;
            }
            catch (Exception ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", sql);
                FEL_LOG_ERROR("sql.sql", "Other error: {0}", ex.Message);

                _lastError = (MySqlErrorCode)(-101);
            }

            return false;
        }

        public bool Execute(PreparedStatement stmt)
        {
            if (_connection == null) return false;

            try
            {
                var now = Time.Now;

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = stmt.CommandText;
                    for (var i = 0; i < stmt.ParameterCount; i++)
                        cmd.Parameters.AddWithValue($"@{i.ToString()}", stmt.Parameters[i]);

                    cmd.ExecuteNonQuery();

                    FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL: {1}", (Time.Now - now).TotalMilliseconds, stmt.GetQueryString());

                    return true;
                }
            }
            catch (MySqlException ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", stmt.GetQueryString());
                FEL_LOG_ERROR("sql.sql", "[{0}] {1}", ex.ErrorCode, ex.Message);

                if (HandleMySqlException(ex.ErrorCode)) // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Execute(stmt); // Try again

                _lastError = ex.ErrorCode;
            }
            catch (Exception ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", stmt.GetQueryString());
                FEL_LOG_ERROR("sql.sql", "Other error: {0}", ex.Message);

                _lastError = (MySqlErrorCode)(-101);
            }

            return false;
        }

        public QueryResult? Query(string sql)
        {
            if (_connection == null) return null;

            try
            {
                var now = Time.Now;

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = sql;

                    var reader = cmd.ExecuteReader();

                    FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL: {1}", (Time.Now - now).TotalMilliseconds, sql);

                    if (!reader.IsClosed && reader.HasRows)
                        return new QueryResult(reader);

                    reader.Close();
                    return null;
                }
            }
            catch (MySqlException ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", sql);
                FEL_LOG_ERROR("sql.sql", "[{0}] {1}", ex.ErrorCode, ex.Message);

                if (HandleMySqlException(ex.ErrorCode)) // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Query(sql); // Try again

                _lastError = ex.ErrorCode;
            }
            catch (Exception ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", sql);
                FEL_LOG_ERROR("sql.sql", "Other error: {0}", ex.Message);

                _lastError = (MySqlErrorCode)(-101);
            }

            return null;
        }

        public PreparedQueryResult? Query(PreparedStatement stmt)
        {
            if (_connection == null) return null;

            try
            {
                var now = Time.Now;

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = stmt.CommandText;
                    for (var i = 0; i < stmt.ParameterCount; i++)
                        cmd.Parameters.AddWithValue($"@{i.ToString()}", stmt.Parameters[i]);

                    var ret = new PreparedQueryResult(cmd.ExecuteReader());
                    FEL_LOG_DEBUG("sql.sql", "[{0} ms] SQL: {1}", (Time.Now - now).TotalMilliseconds, stmt.GetQueryString());

                    return ret.GetRowCount() == 0 ? null : ret;
                }
            }
            catch (MySqlException ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", stmt.GetQueryString());
                FEL_LOG_ERROR("sql.sql", "[{0}] {1}", ex.ErrorCode, ex.Message);

                if (HandleMySqlException(ex.ErrorCode)) // If it returns true, an error was handled successfully (i.e. reconnection)
                    return Query(stmt); // Try again

                _lastError = ex.ErrorCode;
            }
            catch (Exception ex)
            {
                FEL_LOG_INFO("sql.sql", "SQL: {0}", stmt.GetQueryString());
                FEL_LOG_ERROR("sql.sql", "Other error: {0}", ex.Message);

                _lastError = (MySqlErrorCode)(-101);
            }

            return null;
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

        public MySqlErrorCode ExecuteTransaction(SqlTransactionBase transaction)
        {
            if (transaction.Queries.Count == 0)
                return (MySqlErrorCode)(-100);

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
                            var errorCode = _lastError;
                            RollbackTransaction();
                            return errorCode;
                        }
                        break;
                    }
                    case SQL_ELEMENT_RAW:
                    {
                        var sql = query.Element.Query;
                        Assert(!string.IsNullOrEmpty(sql));
                        if (!Execute(sql))
                        {
                            FEL_LOG_WARN("sql.sql", "Transaction aborted. {0} queries not executed.", transaction.Queries.Count);
                            var errorCode = _lastError;
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

        public void EscapeString(ref string str)
        {
            str = MySqlHelper.EscapeString(str);
        }

        public void Ping()
        {
            if (_connection == null) return;

            _connection.Ping();
        }

        public bool LockIfReady()
        {
            return Monitor.TryEnter(_mutex);
        }

        public void Unlock()
        {
            Monitor.Exit(_mutex);
        }

        public string GetServerVersion()
        {
            if (_connection == null) return string.Empty;

            return _connection.ServerVersion;
        }

        private bool HandleMySqlException(MySqlErrorCode error, byte attempts = 5)
        {
            switch (error)
            {
                case MySqlErrorCode.UnableToConnectToHost:
                    FEL_LOG_INFO("sql.sql", "Attempting to reconnect to the MySQL server...");

                    _reconnecting = true;

                    var lErrno = Open();
                    if (lErrno == MySqlErrorCode.None)
                    {
                        // Don't remove 'this' pointer unless you want to skip loading all prepared statements...
                        if (!PrepareStatements())
                        {
                            FEL_LOG_FATAL("sql.sql", "Could not re-prepare statements!");
                            Thread.Sleep(10000);
                            Assert(false);
                        }

                        FEL_LOG_INFO("sql.sql", "Successfully reconnected to {0} @{1}:{2} ({3}).",
                            _connectionInfo.Database, _connectionInfo.Host, _connectionInfo.Port,
                                ((_connectionFlags & ConnectionFlags.CONNECTION_ASYNC) != 0) ? "asynchronous" : "synchronous");

                        _reconnecting = false;
                        return true;
                    }

                    if ((--attempts) == 0)
                    {
                        // Shut down the server when the mysql server isn't
                        // reachable for some time
                        FEL_LOG_FATAL("sql.sql", "Failed to reconnect to the MySQL server, terminating the server to prevent data corruption!");

                        // We could also initiate a shutdown through using std::raise(SIGTERM)
                        Thread.Sleep(10000);
                        Assert(false);
                    }
                    else
                    {
                        // It's possible this attempted reconnect throws 2006 at us.
                        // To prevent crazy recursive calls, sleep here.
                        Thread.Sleep(3000);
                        return HandleMySqlException(lErrno, attempts); // Call self (recursive)
                    }
                    break;
                case MySqlErrorCode.LockDeadlock:
                    return false;    // Implemented in TransactionTask::Execute and DatabaseWorkerPool<T>::DirectCommitTransaction
                // Query related errors - skip query
                case MySqlErrorCode.WrongValueCount:
                case MySqlErrorCode.DuplicateKeyEntry:
                    return false;
                case MySqlErrorCode.BadFieldError:
                case MySqlErrorCode.NoSuchTable:
                    FEL_LOG_ERROR("sql.sql", "Your database structure is not up to date. Please make sure you've executed all queries in the sql/updates folders.");
                    Thread.Sleep(10000);
                    Assert(false);
                    return false;
                case MySqlErrorCode.ParseError:
                    FEL_LOG_ERROR("sql.sql", "Error while parsing SQL. Core fix required.");
                    Thread.Sleep(10000);
                    Assert(false);
                    return false;
                default:
                    FEL_LOG_ERROR("sql.sql", "Unhandled MySQL errno {0}. Unexpected behaviour possible.", error);
                    return false;
            }

            return false;
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

            _disposed = true;
        }
    }

    public class MySqlConnectionProxy<Statements> : MySqlConnectionProxyBase where Statements : unmanaged, Enum
    {
        public MySqlConnectionProxy(MySqlConnectionInfo connectionInfo) : base(connectionInfo)
        {
        }

        //! Constructor for asynchronous connections.
        public MySqlConnectionProxy(ProducerConsumerQueue<SqlOperation> queue, MySqlConnectionInfo connectionInfo) : base(queue, connectionInfo)
        {
        }

        public PreparedStatement GetPreparedStatement(Statements index)
        {
            return GetPreparedStatement(index.AsInteger<Statements, int>());
        }

        protected void PrepareStatement(Statements index, string sql, ConnectionFlags flags)
        {
            PrepareStatement(index.AsInteger<Statements, int>(), sql, flags);
        }
    }
}
