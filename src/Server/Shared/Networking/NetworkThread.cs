// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Threading;
using static Common.Log;
using static Common.Errors;

namespace Server.Shared
{
    public class NetworkThread<TSocketType> where TSocketType : SocketBase, IDisposable
    {
        int _connections;
        volatile bool _stopped;
        object _newSocketsLock = new object();

        Thread? _thread;

        List<TSocketType> _Sockets = new List<TSocketType>();
        List<TSocketType> _newSockets = new List<TSocketType>();

        bool _disposed = false;
        public bool Disposed => _disposed;

        public void Stop()
        {
            _stopped = true;
        }

        public bool Start()
        {
            if (_thread != null)
                return false;

            _thread = new Thread(Run) { Name = $"NetworkThread<{typeof(TSocketType).Name}>" };
            _thread.Start();
            return true;
        }

        public void Wait()
        {
            if (_thread == null)
            {
                Assert(false);
                return;
            }

            _thread.Join();
            _thread = null;
        }

        public int GetConnectionCount()
        {
            return _connections;
        }

        public virtual void AddSocket(TSocketType sock)
        {
            lock(_newSocketsLock)
            {
                Interlocked.Increment(ref _connections);
                _newSockets.Add(sock);
                SocketAdded(sock);
            }
        }

        protected virtual void SocketAdded(TSocketType sock) { }

        protected virtual void SocketRemoved(TSocketType sock) { }

        void AddNewSockets()
        {
            lock(_newSocketsLock)
            {
                if (_newSockets.Count == 0)
                    return;

                foreach (var socket in _newSockets)
                {
                    if (!socket.IsOpen())
                    {
                        SocketRemoved(socket);

                        Interlocked.Decrement(ref _connections);
                    }
                    else
                        _Sockets.Add(socket);
                }

                _newSockets.Clear();
            }
        }

        void Run()
        {
            FEL_LOG_DEBUG("misc", "Network Thread Starting");

            const int sleepTime = 10;
            while (!_stopped)
            {
                Thread.Sleep(sleepTime);

                AddNewSockets();

                _Sockets.RemoveAll(sock => {
                    if (!sock.Update())
                    {
                        if (sock.IsOpen())
                            sock.CloseSocket();

                        SocketRemoved(sock);
                        Interlocked.Decrement(ref _connections);
                        return true;
                    }

                    return false;
                });
            }

            FEL_LOG_DEBUG("misc", "Network Thread exits");
            _newSockets.Clear();
            _Sockets.Clear();
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
                Stop();
                if (_thread != null)
                {
                    Wait();
                    _thread = null;
                }
            }

            _disposed = true;
        }
    }
}
