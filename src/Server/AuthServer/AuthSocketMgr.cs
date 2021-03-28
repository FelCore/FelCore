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
    public class AuthSocketMgr : SocketMgr<AuthSession>
    {
        private static AuthSocketMgr? _instance;

        public static AuthSocketMgr sAuthSocketMgr
        {
            get
            {
                if (_instance == null)
                    _instance = new();

                return _instance;
            }
        }

        public unsafe override bool StartNetwork(string bindIp, int port, int threadCount = 1)
        {
            if (!base.StartNetwork(bindIp, port, threadCount))
                return false;

            _acceptor!.AsyncAcceptWithCallback(&OnSocketAccept);
            return true;
        }

        protected override NetworkThread<AuthSession>[]? CreateThreads()
        {
            return new NetworkThread<AuthSession>[1] { new NetworkThread<AuthSession>() };
        }

        protected static void OnSocketAccept(Socket sock, int threadIndex)
        {
            sAuthSocketMgr.OnSocketOpen(sock, threadIndex);
        }
    }
}
