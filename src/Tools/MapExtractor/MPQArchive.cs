// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using static SharpMPQ.NativeMethods;

namespace Tools.MapExtractor
{
    public class MPQArchive : IDisposable
    {
        public static readonly List<MPQArchive> OpenArchives = new();

        IntPtr _mpq;
        public IntPtr Mpq => _mpq;

        bool _disposed;

        public MPQArchive(string filename)
        {
            int result = libmpq__archive_open(ref _mpq, filename, -1);
            Console.WriteLine("Opening {0}", filename);

            if (result != 0)
            {
                switch (result)
                {
                    case LIBMPQ_ERROR_OPEN:
                        Console.WriteLine("Error opening archive '{0}': Does file really exist?", filename);
                        break;
                    case LIBMPQ_ERROR_FORMAT:            /* bad file format */
                        Console.WriteLine("Error opening archive '{0}': Bad file format", filename);
                        break;
                    case LIBMPQ_ERROR_SEEK:         /* seeking in file failed */
                        Console.WriteLine("Error opening archive '{0}': Seeking in file failed", filename);
                        break;
                    case LIBMPQ_ERROR_READ:              /* Read error in archive */
                        Console.WriteLine("Error opening archive '{0}': Read error in archive", filename);
                        break;
                    case LIBMPQ_ERROR_MALLOC:               /* maybe not enough memory? :) */
                        Console.WriteLine("Error opening archive '{0}': Maybe not enough memory", filename);
                        break;
                    default:
                        Console.WriteLine("Error opening archive '{0}': Unknown error", filename);
                        break;
                }
                return;
            }

            OpenArchives.Add(this);
        }

        ~MPQArchive()
        {
            Dispose(false);
        }

        public void Close()
        {
            if (_mpq != default)
            {
                libmpq__archive_close(_mpq);
                _mpq = default;
            }
        }

        public unsafe void GetFileListTo(List<string> filelist)
        {
            if (libmpq__file_number(_mpq, "(listfile)", out var filenum) != 0) return;

            long size, transferred;
            libmpq__file_size_unpacked(_mpq, filenum, out size);

            IntPtr buffer = Marshal.AllocHGlobal((int)(size + 1));
            ((byte*)buffer)[size] = 0;

            libmpq__file_read(_mpq, filenum, buffer, size, out transferred);

            var files = Marshal.PtrToStringAnsi(buffer);
            if (files != null)
                filelist.AddRange(files.Split('\n', StringSplitOptions.RemoveEmptyEntries));

            Marshal.FreeHGlobal(buffer);
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
