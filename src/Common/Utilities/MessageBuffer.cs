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
        private int _size;

        public MessageBuffer(int initialSize)
        {
            _storage = ArrayPool<byte>.Shared.Rent(initialSize);
            _size = initialSize;
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
            _size = right._size;

            _storage = ArrayPool<byte>.Shared.Rent(_size);
            Buffer.BlockCopy(right._storage, 0, _storage, 0, _size);
        }

        public int Wpos() { return _wpos; }
        public int Rpos() { return _rpos; }

        public byte[] Data()
        {
            return _storage;
        }

        public Span<byte> GetReadSpan(int size = 0) => _storage.AsSpan(_rpos, size <= 0 ? _wpos - _rpos : size);
        public Span<byte> WriteSpan => _storage.AsSpan(_wpos);

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public void Resize(int bytes)
        {
            if (bytes <= 0) return;

            if (_storage.Length >= bytes)
            {
                _size = bytes;
                return;
            }

            var temp = ArrayPool<byte>.Shared.Rent(bytes);
            Buffer.BlockCopy(_storage, 0, temp, 0, _size);

            ArrayPool<byte>.Shared.Return(_storage);

            _storage = temp;
            _size = bytes;
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

        public int GetRemainingSpace() { return _size - _wpos; }

        public int GetBufferSize() { return _size; }

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
                Resize(_size * 3 / 2);
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
