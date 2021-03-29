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
        LOGIN_DEL_EXPIRED_IP_BANS,
        LOGIN_UPD_EXPIRED_ACCOUNT_BANS,
        LOGIN_SEL_IP_INFO,
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
            PrepareStatement(LOGIN_DEL_EXPIRED_IP_BANS, "DELETE FROM ip_banned WHERE unbandate<>bandate AND unbandate<=UNIX_TIMESTAMP()", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_UPD_EXPIRED_ACCOUNT_BANS, "UPDATE account_banned SET active = 0 WHERE active = 1 AND unbandate<>bandate AND unbandate<=UNIX_TIMESTAMP()", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_SEL_IP_INFO, "SELECT unbandate > UNIX_TIMESTAMP() OR unbandate = bandate AS banned, NULL as country FROM ip_banned WHERE ip = ?", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_INS_LOG, "INSERT INTO logs (time, realm, type, level, string) VALUES (?, ?, ?, ?, ?)", CONNECTION_ASYNC);
        }
    }
}
