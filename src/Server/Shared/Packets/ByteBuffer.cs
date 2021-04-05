// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Buffers;
using System.Runtime.InteropServices;
using Common;
using static Common.Errors;

namespace Server.Shared
{
    public class ByteBufferException : Exception
    {
    }

    public class ByteBufferPositionException : ByteBufferException
    {
        public override string Message { get; }

        public ByteBufferPositionException(bool add, int pos, int size, int valueSize)
        {
            var sb = new StringBuilder();
            sb.Append("Attempted to ");
            sb.Append(add ? "put" : "get");
            sb.Append(" value with size: ");
            sb.Append(valueSize);
            sb.Append(" in ByteBuffer (pos: ");
            sb.Append(pos);
            sb.Append(" size: ");
            sb.Append(size);
            sb.Append(")");

            Message = sb.ToString();
        }
    }

    public class ByteBufferSourceException : ByteBufferException
    {
        public override string Message { get; }

        ByteBufferSourceException(int pos, int size, int valueSize)
        {
            var sb = new StringBuilder();
            sb.Append("Attempted to put a ");
            sb.Append(valueSize > 0 ? "NULL-pointer" : "zero-sized value");
            sb.Append(" in ByteBuffer (pos: ");
            sb.Append(pos);
            sb.Append(" size: ");
            sb.Append(size);
            sb.Append(")");

            Message = sb.ToString();
        }
    }

    public class ByteBufferInvalidValueException : ByteBufferException
    {
        public override string Message { get; }

        public ByteBufferInvalidValueException(string type, string value)
        {
            Message = string.Format("Invalid {0} value ({1}) found in ByteBuffer", type, value);
        }
    }

    public unsafe class ByteBuffer : IDisposable
    {
        public const int DEFAULT_SIZE = 0x20;


        private int _wpos;
        private int _rpos;

        private byte[] _storage;

        public ByteBuffer(int initialSize)
        {
            _storage = ArrayPool<byte>.Shared.Rent(initialSize);
        }

        public ByteBuffer() : this(DEFAULT_SIZE)
        {
        }

        public ByteBuffer(ByteBuffer right, bool move = false)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;

            if (move)
            {
                _storage = right.Move();
            }
            else
            {
                var temp = ArrayPool<byte>.Shared.Rent(right._storage.Length);
                Buffer.BlockCopy(right._storage, 0, temp, 0, right._storage.Length);
                _storage = temp;
            }
        }

        public ByteBuffer(MessageBuffer buffer)
        {
            _rpos = 0;
            _wpos = 0;
            _storage = buffer.Move();
        }

        public ByteBuffer(ReadOnlySpan<byte> data)
        {
            _rpos = 0;
            _wpos = data.Length;

            _storage = ArrayPool<byte>.Shared.Rent(_wpos);

            data.CopyTo(_storage);
        }

        ~ByteBuffer()
        {
            Dispose(false);
        }

        public ReadOnlySpan<byte> ReadSpan => _storage.AsSpan(new Range(_rpos, _wpos));

        public ReadOnlySpan<byte> WriteSpan => _storage.AsSpan(0, _wpos);

        public byte[] Move()
        {
            _wpos = 0;
            _rpos = 0;

            var ret = _storage;
            _storage = ArrayPool<byte>.Shared.Rent(1);

            return ret;
        }

        public int Size()
        {
            return _storage.Length;
        }

        public void Resize(int newSize, bool resetPos = true)
        {
            var temp = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.BlockCopy(_storage, 0, temp, 0, _storage.Length > temp.Length ? temp.Length : _storage.Length);

            ArrayPool<byte>.Shared.Return(_storage);

            _storage = temp;

            if (resetPos)
            {
                _rpos = 0;
                _wpos = Size();
            }
        }

        public int this[int pos]
        {
            get
            {
                if (pos > Size())
                    throw new ByteBufferPositionException(false, pos, 1, Size());

                return _storage[pos];
            }
        }

        public int Rpos() { return _rpos; }
        public int Rpos(int pos)
        {
            _rpos = pos;
            return _rpos;
        }

        public int Wpos() { return _wpos; }
        public int Wpos(int pos)
        {
            _wpos = pos;
            return _wpos;
        }

        public void FinishRead()
        {
            _rpos = _wpos;
        }

        public void Reset()
        {
            _wpos = 0;
            _rpos = 0;
        }

        public byte[] Data()
        {
            return _storage;
        }

        public void Clear()
        {
            _storage.AsSpan().Clear(); // Expected it faster than Array.Clear()
            _rpos = _wpos = 0;
        }

        private void EnsureFreeSpace(int addSize)
        {
            Assert(addSize > 0, string.Format("Attempted to put a zero-sized value in ByteBuffer (pos: {0} size: {1})", _wpos, Size()));
            Assert(Size() < 10000000);

            int newSize = _wpos + addSize;
            if (_storage.Length < newSize) // custom memory allocation rules
            {
                if (newSize < 100)
                    Resize(256, false);
                else if (newSize < 750)
                    Resize(2048, false);
                else if (newSize < 6000)
                    Resize(8192, false);
                else
                    Resize(400000, false);
            }

            if (_storage.Length < newSize)
                Resize(newSize, false);
        }

        /// <summary>
        /// Append data to buffer.
        /// </summary>
        /// <param name="src">The data to append.</param>
        public void Append(ReadOnlySpan<byte> src)
        {
            EnsureFreeSpace(src.Length);

            src.CopyTo(_storage.AsSpan(_wpos));

            _wpos += src.Length;
        }

        public void Append(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            var byteCount = Encoding.UTF8.GetByteCount(value);
            EnsureFreeSpace(byteCount);

            Encoding.UTF8.GetBytes(value, _storage.AsSpan(_wpos));
            // Append '\0'
            _storage.AsSpan(_wpos + byteCount)[0] = 0;

            _wpos += byteCount + 1;
        }

        public void Append<T>(T value) where T : unmanaged
        {
            EnsureFreeSpace(sizeof(T));
            MemoryMarshal.Write<T>(_storage.AsSpan(_wpos, sizeof(T)), ref value);
            _wpos += sizeof(T);
        }
        public void Append(ByteBuffer buffer)
        {
            if (buffer.Wpos() > 0)
                Append(buffer.WriteSpan);
        }

        public void Put(int pos, ReadOnlySpan<byte> src)
        {
            Assert(pos >= 0, string.Format("Attempted to put value with invalid pos: {0} in ByteBuffer", pos));
            Assert(pos + src.Length <= Size(), string.Format("Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", src.Length, pos, Size()));

            src.CopyTo(_storage.AsSpan(pos));
        }

        public void Put<T>(int pos, T value) where T : unmanaged
        {
            Assert(pos >= 0, string.Format("Attempted to put value with invalid pos: {0} in ByteBuffer", pos));
            Assert(pos + sizeof(T) <= Size(), string.Format("Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", sizeof(T), pos, Size()));

            MemoryMarshal.Write<T>(_storage.AsSpan(pos), ref value);
        }

        public T Read<T>() where T : unmanaged
        {
            ref T ret = ref Read<T>(_rpos); 
            _rpos += sizeof(T);
            return ret;
        }

        public ref T Read<T>(int pos) where T : unmanaged
        {
            Assert(pos >= 0, string.Format("Attempted to read value with invalid pos: {0} in ByteBuffer", pos));
            if (pos + sizeof(T) > Size())
                throw new ByteBufferPositionException(false, pos, sizeof(T), Size());

            ref var ret = ref MemoryMarshal.AsRef<T>(_storage.AsSpan(pos));

            return ref ret;
        }

        public float ReadFloat()
        {
            var val = Read<float>();
            if (float.IsInfinity(val))
                throw new ByteBufferInvalidValueException("float", "infinity");

            return val;
        }

        public double ReadDouble()
        {
            var val = Read<double>();
            if (double.IsInfinity(val))
                throw new ByteBufferInvalidValueException("double", "infinity");

            return val;
        }

        public string ReadString()
        {
            var index = _rpos;
            var len = 0;

            while (_rpos < Size())
            {
                var c = Read<byte>();

                if (c == 0)
                    break;

                len++;
            }

            if (len > 0)
                return Encoding.UTF8.GetString(_storage, index, len);
            else
                return string.Empty;
        }

        public byte[] ReadBytes(int len)
        {
            if (_rpos + len > Size())
                throw new ByteBufferPositionException(false, _rpos, len, Size());

            byte[] bytes = new byte[len];

            Buffer.BlockCopy(_storage, _rpos, bytes, 0, len);

            _rpos += len;

            return bytes;
        }

        public void ReadBytes(Span<byte> dest)
        {
            if (_rpos + dest.Length > Size())
                throw new ByteBufferPositionException(false, _rpos, dest.Length, Size());

            _storage.AsSpan(_rpos, dest.Length).CopyTo(dest);

            _rpos += dest.Length;
        }

        public void ReadSkip(int skip)
        {
            if (_rpos + skip > Size())
                throw new ByteBufferPositionException(false, _rpos, skip, Size());

            _rpos += skip;
        }

        public void ReadSkip<T>() where T : unmanaged { ReadSkip(sizeof(T)); }
        public void ReadSkipString()
        {
            while (_rpos < Size())
            {
                var c = Read<byte>();

                if (c == 0)
                    break;
            }
        }

        public void PrintStorage()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE ").Append(Size()).Append(" : ");
            for (uint i = 0; i < Size(); ++i)
            {
                sb.Append(_storage[i]).Append(" - ");
            }
            sb.Append(" ");

            Console.WriteLine(sb.ToString());
        }

        public void PrintTextLike()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE ").Append(Size()).Append(" : ");
            for (uint i = 0; i < Size(); ++i)
            {
                sb.Append((char)_storage[i]);
            }
            sb.Append(" ");

            Console.WriteLine(sb.ToString());
        }

        public void PrintHexlike()
        {
            uint j = 1, k = 1;

            StringBuilder sb = new StringBuilder();

            sb.Append("STORAGE_SIZE ").Append(Size()).Append(" : ");

            for (uint i = 0; i < Size(); ++i)
            {
                if ((i == (j * 8)) && ((i != (k * 16))))
                {
                    sb.Append("| ");
                    ++j;
                }
                else if (i == (k * 16))
                {
                    sb.Append("\n");
                    ++k;
                    ++j;
                }

                sb.Append("0x").Append(Read<byte>().ToString("X2")).Append(" ");
            }

            sb.Append(" ");

            Console.WriteLine(sb.ToString());
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
