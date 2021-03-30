// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Net.Sockets;
using Common;
using static Common.Log;
using static Common.ConfigMgr;
using static Common.Errors;
using Server.Database;
using static Server.Database.LoginStatements;
using Server.Shared;

namespace Server.AuthServer
{
    using static eAuthCmd;
    using static Server.AuthServer.AuthResult;

    enum eAuthCmd : byte
    {
        AUTH_LOGON_CHALLENGE = 0x00,
        AUTH_LOGON_PROOF = 0x01,
        AUTH_RECONNECT_CHALLENGE = 0x02,
        AUTH_RECONNECT_PROOF = 0x03,
        REALM_LIST = 0x10,
        XFER_INITIATE = 0x30,
        XFER_DATA = 0x31,
        XFER_ACCEPT = 0x32,
        XFER_RESUME = 0x33,
        XFER_CANCEL = 0x34
    }

    public class AuthSession : SocketBase
    {
        AsyncCallbackProcessor<QueryCallback> _queryProcessor;

        public AuthSession(Socket socket) : base(socket)
        {
            _queryProcessor = new AsyncCallbackProcessor<QueryCallback>();
        }

        public void SendPacket(ByteBuffer packet)
        {
            if (!IsOpen())
                return;

            if (!packet.Empty && packet.Wpos() > 0)
            {
                MessageBuffer buffer = new(packet.Wpos());
                buffer.Write(packet.Data(), packet.Wpos());
                QueuePacket(buffer);
            }
        }

        public override void Start()
        {
            var ipAddress = RemoteAddress.ToString();
            FEL_LOG_TRACE("session", "Accepted connection from {0}", ipAddress);

            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_SEL_IP_INFO);
            stmt.Parameters[0] = ipAddress;

            _queryProcessor.AddCallback(DB.LoginDatabase.AsyncQuery(stmt).WithPreparedCallback(CheckIpCallback));
        }

        void CheckIpCallback(PreparedQueryResult? result)
        {
            if (result != null)
            {
                bool banned = false;
                do
                {
                    var fields = result.Fetch();
                    if (fields.GetUInt64(0) != 0)
                        banned = true;

                } while (result.NextRow());

                if (banned)
                {
                    ByteBuffer pkt = new ByteBuffer();
                    pkt.Append((byte)AUTH_LOGON_CHALLENGE);
                    pkt.Append((byte)0);
                    pkt.Append((byte)WOW_FAIL_BANNED);
                    SendPacket(pkt);
                    FEL_LOG_DEBUG("session", "[AuthSession::CheckIpCallback] Banned ip '{0}:{1}' tries to login!", RemoteAddress.ToString(), RemotePort);
                    return;
                }
            }

            AsyncRead();
        }

        protected override void ReadHandler()
        {
            var packet = GetReadBuffer();
            while (packet.GetActiveSize() != 0)
            {
                var cmd = packet.GetByte(0);
                packet.ReadCompleted(packet.Wpos());
                //TODO:
            }
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            _queryProcessor.ProcessReadyCallbacks();

            return true;
        }
    }
}

