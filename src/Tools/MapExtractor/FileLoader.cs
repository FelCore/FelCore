// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;

namespace Tools.MapExtractor
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public unsafe struct u_map_fcc
    {
        [FieldOffset(0)]
        public fixed byte fcc_txt[4];
        [FieldOffset(0)]
        public uint fcc;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public unsafe struct u_map_fcc_rev
    {
        [FieldOffset(0)]
        public uint fcc;
        [FieldOffset(0)]
        public fixed byte fcc_txt[4];
    }

    //
    // File version chunk
    //
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct file_MVER
    {
        public u_map_fcc_rev map;
        public uint size;
        public uint ver;
    };

    public unsafe class FileLoader : IDisposable
    {
        public const int FILE_FORMAT_VERSION = 18;

        static u_map_fcc MverMagic;

        static FileLoader()
        {
            MverMagic.fcc_txt[0] = (byte)'R';
            MverMagic.fcc_txt[1] = (byte)'E';
            MverMagic.fcc_txt[2] = (byte)'V';
            MverMagic.fcc_txt[3] = (byte)'M';
        }

        byte* _data;
        long _dataSize;
        bool _disposed;

        public virtual bool prepareLoadedData()
        {
            // Check version
            _version = (file_MVER*)_data;
            if (_version->map.fcc != MverMagic.fcc)
                return false;
            if (_version->ver != FILE_FORMAT_VERSION)
                return false;
            return true;
        }

        public byte* GetData() { return _data; }
        public long GetDataSize() { return _dataSize; }

        public file_MVER* _version;
        public file_MVER* Version => _version;

        ~FileLoader()
        {
            Dispose(false);
        }

        public bool LoadFile(string fileName, bool log = true)
        {
            Free();
            MPQFile mf = new(fileName);
            if (mf.IsEof())
            {
                if (log)
                    Console.WriteLine("No such file {0}", fileName);
                return false;
            }

            _dataSize = mf.GetSize();

            _data = (byte*)Marshal.AllocHGlobal((int)_dataSize);
            mf.Read(new Span<byte>(_data, (int)_dataSize), (int)_dataSize);
            mf.Dispose();
            if (prepareLoadedData())
                return true;

            Console.WriteLine("Error loading {0}", fileName);
            mf.Dispose();
            Free();
            return false;
        }

        public virtual void Free()
        {
            if (_data != default)
            {
                Marshal.FreeHGlobal((IntPtr)_data);
                _data = default;
            }

            _data = null;
            _dataSize = 0;
            _version = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            Free();

            _disposed = true;
        }
    }
}
