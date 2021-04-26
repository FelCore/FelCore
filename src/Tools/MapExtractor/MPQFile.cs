// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using static SharpMPQ.NativeMethods;

namespace Tools.MapExtractor
{
    public unsafe class MPQFile : IDisposable
    {

        bool _eof;
        byte* _buffer;
        long _pointer;
        long _size;
        bool _disposed;

        public MPQFile(string filename)    // filenames are not case sensitive
        {
            foreach (var archive in MPQArchive.OpenArchives)
            {
                IntPtr mpq_a = archive.Mpq;

                if (libmpq__file_number(mpq_a, filename, out var filenum) != 0) continue;

                libmpq__file_size_unpacked(mpq_a, filenum, out _size);

                // HACK: in patch.mpq some files don't want to open and give 1 for filesize
                if (_size <= 1)
                {
                    // Console.WriteLine("warning: file {0} has size {1}; cannot read.", filename, _size);
                    _eof = true;
                    _buffer = null;
                    return;
                }

                _buffer = (byte*)Marshal.AllocHGlobal((int)_size);

                libmpq__file_read(mpq_a, filenum, (IntPtr)_buffer, _size, out var transferred);
                return;

            }

            _eof = true;
            _buffer = null;
        }

        ~MPQFile()
        {
            Dispose(false);
        }

        public int Read(Span<byte> dest, int bytes)
        {
            if (_eof) return 0;

            var rpos = _pointer + bytes;
            if (rpos > _size)
            {
                bytes = (int)(_size - _pointer);
                _eof = true;
            }

            new Span<byte>(&_buffer[_pointer], bytes).CopyTo(dest);

            _pointer = rpos;

            return bytes;
        }
        public long GetSize() { return _size; }
        public long GetPos() { return _pointer; }
        public byte* GetBuffer() { return _buffer; }
        public byte* GetPointer() { return _buffer + _pointer; }
        public bool IsEof() { return _eof; }

        public void Seek(int offset)
        {
            _pointer = offset;
            _eof = (_pointer >= _size);
        }

        public void SeekRelative(int offset)
        {
            _pointer += offset;
            _eof = (_pointer >= _size);
        }

        public void Close()
        {
            if (_buffer != default)
            {
                Marshal.FreeHGlobal((IntPtr)_buffer);
                _buffer = default;
            }
            _eof = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            Close();

            _disposed = true;
        }
    }
}
