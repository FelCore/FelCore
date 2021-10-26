// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Common;
using static Common.Errors;
using Cysharp.Text;

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

        private IntPtr _storage;
        private int _size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            return new Span<byte>((void*)_storage, _size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan(int start)
        {
            return new Span<byte>((void*)_storage, _size).Slice(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan(int start, int length)
        {
            return new Span<byte>((void*)_storage, _size).Slice(start, length);
        }

        public ByteBuffer(int initialSize)
        {
            _storage = Marshal.AllocHGlobal(initialSize);
            _size = initialSize;
        }

        public ByteBuffer() : this(DEFAULT_SIZE)
        {
        }

        public ByteBuffer(ByteBuffer right)
        {
            _wpos = right._wpos;
            _rpos = right._rpos;

            _storage = Marshal.AllocHGlobal(right._size);
            _size = right._size;

            Buffer.MemoryCopy((void*)right._storage, (void*)_storage, (long)_size, (long)_size);
        }

        public ByteBuffer(MessageBuffer buffer)
        {
            _rpos = 0;
            _wpos = 0;

            _size = buffer.Wpos();
            _storage = Marshal.AllocHGlobal(_size);

            buffer.Data().AsSpan(0, _size).CopyTo(new Span<byte>((void*)_storage, _size));
        }

        public ByteBuffer(ReadOnlySpan<byte> data)
        {
            _rpos = 0;
            _wpos = data.Length;
            _size = data.Length;

            _storage = Marshal.AllocHGlobal(_size);

            data.CopyTo(AsSpan());
        }

        ~ByteBuffer()
        {
            Dispose(false);
        }

        public static implicit operator ByteBuffer(MessageBuffer buffer) => new ByteBuffer(buffer);

        public ReadOnlySpan<byte> ReadSpan => AsSpan(_rpos, _wpos - _rpos);

        public ReadOnlySpan<byte> WriteSpan => AsSpan(0, _wpos);

        public int Size()
        {
            return _size;
        }

        public void Resize(int newSize, bool resetPos = true)
        {
            _storage = Marshal.ReAllocHGlobal(_storage, (IntPtr)newSize);
            _size = newSize;

            if (resetPos)
            {
                _rpos = 0;
                _wpos = _size;
            }
        }

        public int this[int pos]
        {
            get
            {
                if (pos > Size())
                    throw new ByteBufferPositionException(false, pos, 1, Size());

                return AsSpan()[pos];
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

        public ReadOnlySpan<byte> Data()
        {
            return new ReadOnlySpan<byte>((void*)_storage, _size);
        }

        public void Clear()
        {
            AsSpan().Clear(); // Expected it faster than Array.Clear()
            _rpos = _wpos = 0;
        }

        private void EnsureFreeSpace(int addSize)
        {
            Assert(addSize > 0, "Attempted to put a zero-sized value in ByteBuffer (pos: {0} size: {1})", _wpos, Size());
            Assert(Size() < 10000000);

            int newSize = _wpos + addSize;
            if (_size < newSize) // custom memory allocation rules
            {
                if (newSize < 100)
                    Resize(300, false);
                else if (newSize < 750)
                    Resize(2500, false);
                else if (newSize < 6000)
                    Resize(10000, false);
                else
                    Resize(400000, false);
            }

            if (_size < newSize)
                Resize(newSize, false);
        }

        /// <summary>
        /// Append data to buffer.
        /// </summary>
        /// <param name="src">The data to append.</param>
        public void Append(ReadOnlySpan<byte> src)
        {
            EnsureFreeSpace(src.Length);

            src.CopyTo(AsSpan(_wpos));

            _wpos += src.Length;
        }

        public void Append(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            var byteCount = Encoding.UTF8.GetByteCount(value);
            EnsureFreeSpace(byteCount + 1);

            Encoding.UTF8.GetBytes(value, AsSpan(_wpos));
            // Append '\0'
            AsSpan(_wpos + byteCount)[0] = 0;

            _wpos += byteCount + 1;
        }

        public void Append<T>(T value) where T : unmanaged
        {
            EnsureFreeSpace(sizeof(T));
            MemoryMarshal.Write<T>(AsSpan(_wpos, sizeof(T)), ref value);
            _wpos += sizeof(T);
        }
        public void Append(ByteBuffer buffer)
        {
            if (buffer.Wpos() > 0)
                Append(buffer.WriteSpan);
        }

        public void Put(int pos, ReadOnlySpan<byte> src)
        {
            Assert(pos >= 0, "Attempted to put value with invalid pos: {0} in ByteBuffer", pos);
            Assert(pos + src.Length <= Size(), "Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", src.Length, pos, Size());

            src.CopyTo(AsSpan(pos));
        }

        public void Put<T>(int pos, T value) where T : unmanaged
        {
            Assert(pos >= 0, "Attempted to put value with invalid pos: {0} in ByteBuffer", pos);
            Assert(pos + sizeof(T) <= Size(), "Attempted to put value with size: {0} in ByteBuffer (pos: {1} size: {2})", sizeof(T), pos, Size());

            MemoryMarshal.Write<T>(AsSpan(pos), ref value);
        }

        public T Read<T>() where T : unmanaged
        {
            ref T ret = ref Read<T>(_rpos); 
            _rpos += sizeof(T);
            return ret;
        }

        public ref T Read<T>(int pos) where T : unmanaged
        {
            Assert(pos >= 0, "Attempted to read value with invalid pos: {0} in ByteBuffer", pos);
            if (pos + sizeof(T) > Size())
                throw new ByteBufferPositionException(false, pos, sizeof(T), Size());

            ref var ret = ref MemoryMarshal.AsRef<T>(AsSpan(pos));

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
                return Encoding.UTF8.GetString(AsSpan(index, len));
            else
                return string.Empty;
        }

        public byte[] ReadBytes(int len)
        {
            if (_rpos + len > Size())
                throw new ByteBufferPositionException(false, _rpos, len, Size());

            byte[] bytes = new byte[len];

            AsSpan(_rpos, len).CopyTo(bytes);

            _rpos += len;

            return bytes;
        }

        public void ReadBytes(Span<byte> dest)
        {
            if (_rpos + dest.Length > Size())
                throw new ByteBufferPositionException(false, _rpos, dest.Length, Size());

            AsSpan(_rpos, dest.Length).CopyTo(dest);

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
            using(var sb = ZString.CreateStringBuilder(true))
            {
                sb.Append("STORAGE_SIZE ");
                sb.Append(Size());
                sb.Append(" : ");
                for (uint i = 0; i < Size(); ++i)
                {
                    sb.Append(((byte*)_storage)[i]);
                    sb.Append(" - ");
                }
                sb.Append(" ");

                Console.WriteLine(sb.ToString());
            }
        }

        public void PrintTextLike()
        {
            using(var sb = ZString.CreateStringBuilder(true))
            {
                sb.Append("STORAGE_SIZE ");
                sb.Append(Size());
                sb.Append(" : ");
                for (uint i = 0; i < Size(); ++i)
                {
                    sb.Append((char)((byte*)_storage)[i]);
                }
                sb.Append(" ");

                Console.WriteLine(sb.ToString());
            }
        }

        public void PrintHexlike()
        {
            uint j = 1, k = 1;

            using(var sb = ZString.CreateStringBuilder(true))
            {
                sb.Append("STORAGE_SIZE ");
                sb.Append(Size());
                sb.Append(" : ");

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

                    sb.Append("0x");
                    sb.Append(Read<byte>().ToString("X2"));
                    sb.Append(" ");
                }

                sb.Append(" ");

                Console.WriteLine(sb.ToString());
            }
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
            _storage = IntPtr.Zero;
            _size = 0;

            _disposed = true;
        }
    }
}
