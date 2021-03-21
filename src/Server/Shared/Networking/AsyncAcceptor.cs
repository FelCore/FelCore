// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Net;
using System.Net.Sockets;
using static Common.Log;

namespace Server.Shared
{
    public delegate int ThreadIndexFactory();

    public class AsyncAcceptor
    {
        TcpListener? _listener;
        volatile bool _closed;

        string _ip;
        int _port;

        ThreadIndexFactory _threadIndexFactory = () => 0;

        AsyncCallback _internalAsyncCallback;

        unsafe delegate* managed<Socket, int, void> _acceptCallback;

        public AsyncAcceptor(string ip, int port)
        {
            _ip = ip;
            _port = port;

            _internalAsyncCallback = InternalAsyncCallback;
        }

        public void SetThreadIndexFactory(ThreadIndexFactory factory)
        {
            _threadIndexFactory = factory;
        }

        public bool Bind()
        {
            if (!IPAddress.TryParse(_ip, out var bindIp))
            {
                FEL_LOG_FATAL("network", "Could not bind to ip {0}.", _ip);
                return false;
            }

            try
            {
                _listener = new TcpListener(bindIp, _port);
                _listener.Start();
            }
            catch (ArgumentOutOfRangeException)
            {
                FEL_LOG_FATAL("network", "Could not bind to port {0}.", _port);
                return false;
            }
            catch (SocketException ex)
            {
                FEL_LOG_FATAL("network", "Failed to start listening on {0}:{1} {2}", _ip, _port, ex.Message);
                return false;
            }

            return true;
        }

        unsafe void InternalAsyncCallback(IAsyncResult result)
        {
            if (_listener == null) return;

            try
            {
                var socket = _listener.EndAcceptSocket(result);
                socket.Blocking = false;

                var threadIndex = _threadIndexFactory();
                _acceptCallback(socket, threadIndex);
            }
            catch (Exception ex)
            {
                FEL_LOG_INFO("network", "Failed to initialize client's socket {0} in InternalAsyncCallback", ex.Message);
            }
            finally
            {
                if (!_closed)
                    AsyncAcceptWithCallback(_acceptCallback);
            }
        }

        public unsafe void AsyncAcceptWithCallback(delegate* managed<Socket, int, void> callback)
        {
            if (_listener == null)
            {
                FEL_LOG_ERROR("network", "AsyncAcceptWithCallback() is called after AsyncAcceptor is closed!");
                return;
            }

            try
            {
                _acceptCallback = callback;
                _listener.BeginAcceptSocket(_internalAsyncCallback, null);
            }
            catch (ObjectDisposedException ex)
            {
                FEL_LOG_FATAL("network", "Async accept socket has error {0}", ex.Message);
            }
        }

        public void InternalAsyncCallback2<T>(IAsyncResult result) where T : INetSession, new()
        {
            if (_listener == null) return;

            try
            {
                var socket = _listener.EndAcceptSocket(result);
                socket.Blocking = false;

                new T().Start(socket);
            }
            catch (Exception ex)
            {
                FEL_LOG_INFO("network", "Failed to initialize client's socket {0} in InternalAsyncCallback2", ex.Message);
            }
            finally
            {
                if (!_closed)
                    AsyncAccept<T>();
            }
        }

        public void AsyncAccept<T>() where T : INetSession, new()
        {
            if (_listener == null)
            {
                FEL_LOG_ERROR("network", "AsyncAccept<T>() is called after AsyncAcceptor is closed!");
                return;
            }

            try
            {
                _listener.BeginAcceptSocket(InternalAsyncCallback2<T> , null);
            }
            catch (ObjectDisposedException ex)
            {
                FEL_LOG_FATAL("network", "Async accept socket has error {0}", ex.Message);
            }
        }

        public void Close()
        {
            if (_closed)
                return;

            _closed = true;

            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }
        }
    }
}
