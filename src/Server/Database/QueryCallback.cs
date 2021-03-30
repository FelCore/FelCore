// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using Common;
using static Common.Errors;

namespace Server.Database
{
    public class QueryCallback : ISqlCallback
    {
        private bool _isPrepared;

        public QueryCallback(Future<QueryResult?> result)
        {
            _isPrepared = false;
            _rawString = result;
        }

        public QueryCallback(Future<PreparedQueryResult?> result)
        {
            _isPrepared = true;
            _prepared = result;
        }

        public QueryCallback WithCallback(Action<QueryResult?> callback)
        {
            return WithChainingCallback((queryCallback, result) => callback(result));
        }

        public QueryCallback WithPreparedCallback(Action<PreparedQueryResult?> callback)
        {
            return WithChainingPreparedCallback((queryCallback, result) => callback(result));
        }

        public QueryCallback WithChainingCallback(Action<QueryCallback, QueryResult?> callback)
        {
            Assert(_callbacks.Count > 0 || !_isPrepared, "Attempted to set callback function for string query on a prepared async query");
            _callbacks.Enqueue(new QueryCallbackData(callback));
            return this;
        }

        public QueryCallback WithChainingPreparedCallback(Action<QueryCallback, PreparedQueryResult?> callback)
        {
            Assert(_callbacks.Count > 0 || _isPrepared, "Attempted to set callback function for prepared query on a string async query");
            _callbacks.Enqueue(new QueryCallbackData(callback));
            return this;
        }

        static void MoveFrom(QueryCallback to, QueryCallback from)
        {
            Assert(to._isPrepared == from._isPrepared);

            if (!to._isPrepared)
            {
                to._rawString = from._rawString;
                from._rawString = null;
            }
            else
            {
                to._prepared = from._prepared;
                from._prepared = null;
            }
        }

        public void SetNextQuery(QueryCallback next)
        {
            MoveFrom(this, next);
        }

        public bool InvokeIfReady()
        {
            QueryCallbackData callback = _callbacks.Peek();

            var checkStateAndReturnCompletion = new Func<bool>(() =>
            {
                _callbacks.Dequeue();

                bool hasNext = !_isPrepared ? _rawString != null && _rawString.Valid : _prepared != null && _prepared.Valid;
                if (_callbacks.Count == 0)
                {
                    Assert(!hasNext);
                    return true;
                }

                // abort chain
                if (!hasNext)
                    return true;

                Assert(_isPrepared == _callbacks.Peek().IsPrepared);
                return false;
            });

            if (!_isPrepared)
            {
                if (_rawString != null)
                {
                    if (_rawString.Valid && _rawString.Wait(0))
                    {
                        Future<QueryResult?> f = _rawString;
                        _rawString = null;

                        Action<QueryCallback, QueryResult?> cb = callback.RawString!;
                        callback.RawString = null;

                        cb(this, f.Result);

                        return checkStateAndReturnCompletion();
                    }
                }
            }
            else
            {
                if (_prepared != null)
                {
                    if (_prepared.Valid && _prepared.Wait(0))
                    {
                        Future<PreparedQueryResult?> f = _prepared;
                        _prepared = null;

                        Action<QueryCallback, PreparedQueryResult?> cb = callback.Prepared!;
                        callback.Prepared = null;

                        cb(this, f.Result);

                        return checkStateAndReturnCompletion();
                    }
                }
            }

            return false;
        }

        Future<QueryResult?>? _rawString;
        Future<PreparedQueryResult?>? _prepared;
        Queue<QueryCallbackData> _callbacks = new Queue<QueryCallbackData>();
    }

    struct QueryCallbackData
    {
        public QueryCallbackData(Action<QueryCallback, QueryResult?> callback)
        {
            RawString = callback;
            Prepared = null;
            _isPrepared = false;
        }

        public QueryCallbackData(Action<QueryCallback, PreparedQueryResult?> callback)
        {
            RawString = null;
            Prepared = callback;
            _isPrepared = true;
        }

        private bool _isPrepared;
        public bool IsPrepared => _isPrepared;
        public Action<QueryCallback, QueryResult?>? RawString;
        public Action<QueryCallback, PreparedQueryResult?>? Prepared;
    }
}
