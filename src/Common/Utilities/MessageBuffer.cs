// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Buffers;

namespace Common
{
    public unsafe class MessageBuffer : IDisposable
    {
        public const int DefaultSize = 0x20;

        private int _wpos;
        private int _rpos;

        private byte[] _storage;

        public MessageBuffer(int initialSize)
        {
            _storage = ArrayPool<byte>.Shared.Rent(initialSize);
        }

        ~MessageBuffer()
        {
            Dispose(false);
        }

        public MessageBuffer() : this(DefaultSize) { }

        public MessageBuffer(MessageBuffer right)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;

            _storage = ArrayPool<byte>.Shared.Rent(right._storage.Length);
            Buffer.BlockCopy(right._storage, 0, _storage, 0, right._storage.Length);
        }

        public byte[] Move()
        {
            _wpos = 0;
            _rpos = 0;

            var ret = _storage;
            _storage = ArrayPool<byte>.Shared.Rent(1);

            return ret;
        }

        public int Wpos() { return _wpos; }
        public int Rpos() { return _rpos; }

        public byte[] Data()
        {
            return _storage;
        }

        public ReadOnlySpan<byte> ReadSpan => _storage.AsSpan(new Range(_rpos, _wpos));
        public Span<byte> WriteSpan => _storage.AsSpan(_wpos);

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public void Resize(int bytes)
        {
            var temp = ArrayPool<byte>.Shared.Rent(bytes);
            Buffer.BlockCopy(_storage, 0, temp, 0, _storage.Length > temp.Length ? temp.Length : _storage.Length);

            ArrayPool<byte>.Shared.Return(_storage);

            _storage = temp;
        }

        public void ReadCompleted(int bytes)
        {
            _rpos += bytes;

            if (_wpos == 0)
                _rpos = 0;
        }

        public void WriteCompleted(int bytes) { _wpos += bytes; }

        public int GetActiveSize()
        {
            if (_rpos > _wpos)
                return 0;

            return _wpos - _rpos;
        }

        public int GetRemainingSpace() { return _storage.Length - _wpos; }

        public int GetBufferSize() { return _storage.Length; }

        // Discards inactive data
        public void Normalize()
        {
            if (_rpos > 0 && _wpos >= _rpos)
            {
                if (_rpos != _wpos)
                    Buffer.BlockCopy(_storage, _rpos, _storage, 0, GetActiveSize());

                _wpos -= _rpos;
                _rpos = 0;
            }
        }

        // Ensures there's "some" free space, make sure to call Normalize() before this
        public void EnsureFreeSpace()
        {
            // resize buffer if it's already full
            if (GetRemainingSpace() == 0)
                Resize(_storage.Length * 3 / 2);
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            data.CopyTo(_storage.AsSpan(_wpos));
            WriteCompleted(data.Length);
        }

        bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
            }

            ArrayPool<byte>.Shared.Return(_storage);

            _disposed = true;
        }
    }
}
