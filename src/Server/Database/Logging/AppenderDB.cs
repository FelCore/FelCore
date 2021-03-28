// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using Common;
using static Common.AppenderType;
using static Server.Database.LoginStatements;

namespace Server.Database
{
    public class AppenderDB : Appender
    {
        public static new AppenderType Type => APPENDER_DB;

        public AppenderDB(byte id, string name, LogLevel level, AppenderFlags flags, string[] args)
            : base(id, name, level, flags)
        {

        }

        public override void SetRealmId(uint realmId)
        {
            _enabled = true;
            _realmId = realmId;
        }

        uint _realmId;
        bool _enabled;

        protected override void _Write(ref LogMessage message)
        {
            // Avoid infinite loop, PExecute triggers Logging with "sql.sql" type
            if (!_enabled || message.Type.Contains("sql"))
                return;

            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_INS_LOG);

            stmt.Parameters[0] = message.MTime;
            stmt.Parameters[1] = _realmId;
            stmt.Parameters[2] = message.Type;
            stmt.Parameters[3] = (byte)message.Level;
            stmt.Parameters[4] = message.Text;

            DB.LoginDatabase.Execute(stmt);
        }
    }
}
