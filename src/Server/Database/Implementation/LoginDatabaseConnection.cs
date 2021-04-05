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
        LOGIN_SEL_LOGONCHALLENGE,
        LOGIN_INS_ACCOUNT,
        LOGIN_INS_REALM_CHARACTERS_INIT,
        LOGIN_UPD_LOGONPROOF,
        LOGIN_INS_FALP_IP_LOGGING,
        LOGIN_UPD_FAILEDLOGINS,
        LOGIN_INS_ACCOUNT_AUTO_BANNED,
        LOGIN_INS_IP_AUTO_BANNED,
        LOGIN_SEL_REALM_CHARACTER_COUNTS,
        LOGIN_SEL_RECONNECTCHALLENGE,

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
            PrepareStatement(LOGIN_SEL_LOGONCHALLENGE, "SELECT a.id, a.username, a.locked, a.lock_country, a.last_ip, a.failed_logins, ab.unbandate > UNIX_TIMESTAMP() OR ab.unbandate = ab.bandate, " +
                "ab.unbandate = ab.bandate, aa.SecurityLevel, a.totp_secret, a.salt, a.verifier " +
                "FROM account a LEFT JOIN account_access aa ON a.id = aa.AccountID LEFT JOIN account_banned ab ON ab.id = a.id AND ab.active = 1 WHERE a.username = ?", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_SEL_RECONNECTCHALLENGE, "SELECT a.id, UPPER(a.username), a.locked, a.lock_country, a.last_ip, a.failed_logins, ab.unbandate > UNIX_TIMESTAMP() OR ab.unbandate = ab.bandate, " +
                "ab.unbandate = ab.bandate, aa.SecurityLevel, a.session_key " +
                "FROM account a LEFT JOIN account_access aa ON a.id = aa.AccountID LEFT JOIN account_banned ab ON ab.id = a.id AND ab.active = 1 WHERE a.username = ? AND a.session_key IS NOT NULL", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_INS_ACCOUNT, "INSERT INTO account(username, salt, verifier, reg_mail, email, joindate) VALUES(?, ?, ?, ?, ?, NOW())", CONNECTION_SYNCH);
            PrepareStatement(LOGIN_INS_REALM_CHARACTERS_INIT, "INSERT INTO realmcharacters (realmid, acctid, numchars) SELECT realmlist.id, account.id, 0 FROM realmlist, account LEFT JOIN realmcharacters ON acctid = account.id WHERE acctid IS NULL", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_UPD_LOGONPROOF, "UPDATE account SET session_key = ?, last_ip = ?, last_login = NOW(), locale = ?, failed_logins = 0, os = ? WHERE username = ?", CONNECTION_SYNCH);
            PrepareStatement(LOGIN_INS_FALP_IP_LOGGING, "INSERT INTO logs_ip_actions (account_id, character_guid, realm_id, type, ip, systemnote, unixtime, time) VALUES (?, 0, 0, 1, ?, ?, unix_timestamp(NOW()), NOW())", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_UPD_FAILEDLOGINS, "UPDATE account SET failed_logins = failed_logins + 1 WHERE username = ?", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_INS_ACCOUNT_AUTO_BANNED, "INSERT INTO account_banned (id, bandate, unbandate, bannedby, banreason, active) VALUES (?, UNIX_TIMESTAMP(), UNIX_TIMESTAMP()+?, 'Fel Auth', 'Failed login autoban', 1)", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_INS_IP_AUTO_BANNED, "INSERT INTO ip_banned (ip, bandate, unbandate, bannedby, banreason) VALUES (?, UNIX_TIMESTAMP(), UNIX_TIMESTAMP()+?, 'Fel Auth', 'Failed login autoban')", CONNECTION_ASYNC);
            PrepareStatement(LOGIN_SEL_REALM_CHARACTER_COUNTS, "SELECT realmid, numchars FROM realmcharacters WHERE  acctid = ?", CONNECTION_ASYNC);
        }
    }
}
