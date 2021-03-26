// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;

namespace Server.Database
{
    public interface ISqlCallback
    {
        bool InvokeIfReady();
    }

    public class AsyncCallbackProcessor<T> where T : ISqlCallback
    {
        List<T> _callbacks = new List<T>();

        public T AddCallback(T query)
        {
            _callbacks.Add(query);
            return query;
        }

        public void ProcessReadyCallbacks()
        {
            if (_callbacks.Count == 0)
                return;

            _callbacks.RemoveAll(callback => callback.InvokeIfReady());
        }
    }
}
