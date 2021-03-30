// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;

namespace Common
{
    public class Future<TResult>
    {
        private bool _valid;

        public bool Valid => _valid;

        private TResult? _result;
        public TResult Result
        {
            get
            {
                Wait();
                return _result!;
            }
        }

        private object _lock = new object();

        private volatile bool _resultWasSet;

        internal Future() {}

        internal void SetValid()
        {
            _valid = true;
        }

        internal bool TrySetResult(TResult result)
        {
            if (!_valid)
                throw new InvalidOperationException("This future is invalid!");

            lock(_lock)
            {
                if (_resultWasSet)
                    return false;

                _result = result;
                _resultWasSet = true;

                Monitor.PulseAll(_lock);
                return true;
            }
        }

        public void Wait()
        {
            if (!_valid)
                throw new InvalidOperationException("This future is invalid!");

            if (_resultWasSet)
                return;

            lock(_lock)
            {
                Monitor.Wait(_lock);
            }
        }

        public bool Wait(int millisecondsTimeout)
        {
            if (!_valid)
                throw new InvalidOperationException("This future is invalid!");

            if (_resultWasSet)
                return true;

            lock(_lock)
            {
                Monitor.Wait(_lock, millisecondsTimeout);

                return _resultWasSet;
            }
        }
    }
}
