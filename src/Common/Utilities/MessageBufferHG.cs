// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;

namespace Common
{
    public unsafe class MessageBufferHG : IDisposable
    {
        public const int DefaultSize = 0x20;

        private int _wpos;
        private int _rpos;

        private IntPtr _storage;
        private int _size;

        public MessageBufferHG(int initialSize)
        {
            _storage = Marshal.AllocHGlobal(initialSize);
            _size = initialSize;
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

            _storage = Marshal.AllocHGlobal(right._size);
            _size = right._size;

            Buffer.MemoryCopy((void*)right._storage, (void*)_storage, (long)_size, (long)_size);
        }

        public IntPtr Move()
        {
            _wpos = 0;
            _rpos = 0;
            _size = 0;

            var ret = _storage;
            _storage = IntPtr.Zero;

            return ret;
        }

        public int Wpos() { return _wpos; }
        public int Rpos() { return _rpos; }

        public ReadOnlySpan<byte> Data() => new ReadOnlySpan<byte>((void*)_storage, _size);

        public ReadOnlySpan<byte> ReadSpan => new ReadOnlySpan<Byte>((byte*)_storage + _rpos, _wpos - _rpos);
        public Span<byte> WriteSpan => new Span<byte>((void*)_storage, _size).Slice(_wpos);

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public void Resize(int bytes)
        {
            _storage = Marshal.ReAllocHGlobal(_storage, (IntPtr)bytes);
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
                {
                    long size = (long)GetActiveSize();
                    Buffer.MemoryCopy((byte*)_storage + _rpos, (byte*)_storage, size, size);
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

            Marshal.FreeHGlobal(_storage);

            _disposed = true;
        }
    }
}
