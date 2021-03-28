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
using Server.Shared;

namespace Server.AuthServer
{
    public class AuthSession : SocketBase
    {
        AsyncCallbackProcessor<QueryCallback> _queryProcessor;

        public AuthSession(Socket socket) : base(socket)
        {
            _queryProcessor = new AsyncCallbackProcessor<QueryCallback>();
        }

        public override void Start()
        {
            var ipAddress = RemoteAddress.ToString();
            FEL_LOG_TRACE("session", "Accepted connection from {0}", ipAddress);
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

