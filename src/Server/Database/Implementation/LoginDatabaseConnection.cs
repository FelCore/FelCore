// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using Common;

namespace Server.Database
{
    using static LoginStatements;
    using static Database.ConnectionFlags;

    public enum LoginStatements : int
    {
        LOGIN_SEL_REALMLIST,
        LOGIN_INS_LOG,
        MAX_LOGINDATABASE_STATEMENTS
    }

    public class LoginDatabaseConnection : MySqlConnectionProxy<LoginStatements>
    {
        public LoginDatabaseConnection(MySqlConnectionInfo connectionInfo) : base(connectionInfo)
        {
        }

        //! Constructor for asynchronous connections.
        public LoginDatabaseConnection(ProducerConsumerQueue<SqlOperation> queue, MySqlConnectionInfo connectionInfo) : base(queue, connectionInfo)
        {
        }

        protected override void DoPrepareStatements()
        {
            if (!_reconnecting)
                Array.Resize(ref _preparedStatementQueries, (int)MAX_LOGINDATABASE_STATEMENTS);

            PrepareStatement(LOGIN_SEL_REALMLIST, "SELECT id, name, address, localAddress, localSubnetMask, port, icon, flag, timezone, allowedSecurityLevel, population, gamebuild FROM realmlist WHERE flag <> 3 ORDER BY name", CONNECTION_SYNCH);
            PrepareStatement(LOGIN_INS_LOG, "INSERT INTO logs (time, realm, type, level, string) VALUES (?, ?, ?, ?, ?)", CONNECTION_ASYNC);
        }
    }
}
