// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System.Net.Sockets;

namespace Server.Shared
{
    public interface INetSession
    {
        void Start(Socket socket);
    }
}
