// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;
using Common;

namespace Server.Database
{
    class DatabaseWorker : IDisposable
    {
        Thread _workerThread;
        InterlockedBoolean _cancelationToken;
        ProducerConsumerQueue<SqlOperation> _queue;
        MySqlConnection _connection;

        private bool _disposed;

        public DatabaseWorker(ProducerConsumerQueue<SqlOperation> newQueue, MySqlConnection connection)
        {
            _queue = newQueue;
            _connection = connection;

            _workerThread = new Thread(WorkerThread) { Name = "DB Worker Thread", IsBackground = true };
            _workerThread.Start();
        }

        void WorkerThread()
        {
            if (_queue == null)
                return;

            for (;;)
            {
                SqlOperation? operation;

                _queue.WaitAndPop(out operation);

                if (_cancelationToken || operation == null)
                    return;

                operation.SetConnection(_connection);
                operation.Call();
            }
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
                if (!_cancelationToken.Exchange(true))
                {
                    _queue.Cancel();
                    _workerThread.Join();
                }
            }

            _disposed = true;
        }
    }
}
