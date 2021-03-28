// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Common;
using static Common.Log;
using static Common.Errors;

namespace Server.Shared
{
    public class SocketMgr<SocketType> where SocketType : SocketBase
    {
        protected AsyncAcceptor? _acceptor;
        protected NetworkThread<SocketType>[]? _threads;
        protected int _threadCount;

        protected SocketMgr() {}

        public virtual bool StartNetwork(string bindIp, int port, int threadCount = 1)
        {
            Assert(threadCount > 0);

            var acceptor = new AsyncAcceptor(bindIp, port);
            if (!acceptor.Bind())
            {
                FEL_LOG_ERROR("network", "StartNetwork failed to bind socket acceptor");
                return false;
            }

            _acceptor = acceptor;
            _threadCount = threadCount;
            _threads = CreateThreads();

            if (_threads == null)
            {
                Assert(false);
                return false;
            }

            for (int i = 0; i < _threadCount; ++i)
                _threads[i].Start();

            _acceptor.SetThreadIndexFactory(() => {
                return SelectThreadWithMinConnections();
            });

            return true;
        }

        public virtual void StopNetwork()
        {
            if (_acceptor == null || _threads == null)
            {
                Assert(false);
                return;
            }

            _acceptor.Close();

            if (_threadCount != 0)
                for (int i = 0; i < _threadCount; ++i)
                    _threads[i].Stop();

            Wait();

            _acceptor = null;
            _threads = null;
            _threadCount = 0;
        }

        void Wait()
        {
            if (_threads == null)
            {
                Assert(false);
                return;
            }

            if (_threadCount != 0)
                for (int i = 0; i < _threadCount; ++i)
                    _threads[i].Wait();
        }

        public virtual void OnSocketOpen(Socket sock, int threadIndex)
        {
            try
            {
                var newSocket = Activator.CreateInstance(typeof(SocketType), sock) as SocketType;
                if (newSocket == null)
                {
                    Assert(false);
                    return;
                }

                newSocket.Start();

                if (_threads == null)
                {
                    Assert(false);
                    return;
                }

                _threads[threadIndex].AddSocket(newSocket);
            }
            catch (Exception ex)
            {
                FEL_LOG_WARN("network", "Failed to retrieve client's remote address {0}", ex.Message);
            }
        }

        protected virtual NetworkThread<SocketType>[]? CreateThreads() { return null; }

        public int GetNetworkThreadCount() { return _threadCount; }

        int SelectThreadWithMinConnections()
        {
            if (_threads == null)
            {
                Assert(false);
                return 0;
            }

            int min = 0;

            for (int i = 1; i < _threadCount; ++i)
                if (_threads[i].GetConnectionCount() < _threads[min].GetConnectionCount())
                    min = i;

            return min;
        }
    }
}
