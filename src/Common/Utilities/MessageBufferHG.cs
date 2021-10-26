// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Common
{
    public unsafe class MessageBufferHG : IDisposable
    {
        public const int DefaultSize = 0x20;

        private int _wpos;
        private int _rpos;

        private void* _storage;
        private uint _capacity;
        private int _size;

        public MessageBufferHG(int initialSize)
        {
            if (initialSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialSize));

            _size = initialSize;
            _capacity = BitOperations.RoundUpToPowerOf2((uint)_size);

            _storage = NativeMemory.Alloc(_capacity);
        }

        ~MessageBufferHG()
        {
            Dispose(false);
        }

        public MessageBufferHG() : this(DefaultSize) { }

        public MessageBufferHG(MessageBufferHG right)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;
            _size = right._size;
            _capacity = BitOperations.RoundUpToPowerOf2((uint)_size);

            _storage = NativeMemory.Alloc(_capacity);

            Buffer.MemoryCopy(right._storage, _storage, (ulong)_size, (ulong)_size);
        }

        public int Wpos() { return _wpos; }
        public int Rpos() { return _rpos; }

        public ReadOnlySpan<byte> Data() => new ReadOnlySpan<byte>(_storage, _size);

        public Span<byte> GetReadSpan(int size = 0) => new Span<byte>((byte*)_storage + _rpos, size <= 0 ? _wpos - _rpos : size);

        public Span<byte> WriteSpan => new Span<byte>(_storage, _size).Slice(_wpos);

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public void Resize(int bytes)
        {
            if (bytes <= 0) return;

            if (_capacity >= bytes)
            {
                _size = bytes;
                return;
            }

            _size = bytes;
            _capacity = BitOperations.RoundUpToPowerOf2((uint)_size);

            _storage = NativeMemory.Realloc(_storage, _capacity);
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
                {
                    var size = (ulong)GetActiveSize();
                    Buffer.MemoryCopy((byte*)_storage + _rpos, _storage, size, size);
                }

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
            data.CopyTo(WriteSpan);
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

            NativeMemory.Free(_storage);

            _disposed = true;
        }
    }
}
