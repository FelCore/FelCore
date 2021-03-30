// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;

namespace Common
{
    public struct Promise<TResult>
    {
        private Future<TResult>? _future;

        public Future<TResult> GetFuture()
        {
            if (_future == null)
            {
                _future = new Future<TResult>();
                _future.SetValid();
            }

            return _future;
        }

        public void SetResult(TResult result)
        {
            if (!TrySetResult(result))
                throw new InvalidOperationException("Future result is already set!");
        }

        public bool TrySetResult(TResult result)
        {
            if (_future == null)
                throw new InvalidOperationException("Fucture object has not been created yet!");

            return _future.TrySetResult(result);
        }
    }
}
