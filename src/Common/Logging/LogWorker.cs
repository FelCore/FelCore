// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;

namespace Common
{
    public class LogWorker : IDisposable
    {
        Thread[] _workerThreads;
        volatile bool _cancelationToken;
        ProducerConsumerQueue<LogOperation> _queue;

        private bool _disposed;

        public LogWorker(ProducerConsumerQueue<LogOperation> newQueue, int threads = 1)
        {
            _queue = newQueue;
            _cancelationToken = false;

            _workerThreads = new Thread[threads <= 0 ? 1 : threads];

            for(int i = 0; i < threads; i++)
            {
                _workerThreads[i] = new Thread(WorkerThread) { Name = $"Log Worker Thread#{i}" };
                _workerThreads[i].Start();
            }
        }

        void WorkerThread()
        {
            if (_queue == null)
                return;

            for (; ; )
            {
                LogOperation? operation;

                _queue.WaitAndPop(out operation);

                if (_cancelationToken || operation == null)
                    return;

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
                _cancelationToken = true;
                _queue.Cancel();

                foreach(var thread in _workerThreads)
                    thread.Join();
            }

            _disposed = true;
        }
    }
}
