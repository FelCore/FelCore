// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Numerics;
using System.Buffers;
using System.Security.Cryptography;

namespace Common
{
    public unsafe sealed class SHA1Hash : IDisposable
    {
        private SHA1 _sha1;

        public const int SHA1_DIGEST_LENGTH = 20;

        private bool _disposed;

        public SHA1Hash()
        {
            _sha1 = SHA1.Create();
        }

        public void UpdateBigNumbers(params BigInteger[] bigIntegers)
        {
            foreach(var bn in bigIntegers)
                UpdateData(bn.ToByteArray(true));
        }

        public void UpdateData(byte[] data)
        {
            UpdateData(data, data.Length);
        }

        public void UpdateData(byte[] data, int length, int startIndex = 0)
        {
            _sha1.TransformBlock(data, 0, length, null, 0);
        }

        public void UpdateData(string str)
        {
            if (string.IsNullOrEmpty(str))
                return;

            var bufferSize = Encoding.UTF8.GetByteCount(str);
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
            UpdateData(buffer, bufferSize);

            ArrayPool<byte>.Shared.Return(buffer);
        }

        public void Initialize()
        {
            _sha1.Initialize();
        }

        public void Finish()
        {
            _sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        }

        public bool ComputeHash(string str, Span<byte> hash, out int byteCount)
        {
            if (string.IsNullOrEmpty(str))
            {
                byteCount = 0;
                return false;
            }

            var bufferSize = Encoding.UTF8.GetByteCount(str);

            if (bufferSize <= Util.MaxStackLimit)
            {
                Span<byte> buffer = stackalloc byte[bufferSize];
                Encoding.UTF8.GetBytes(str, buffer);
                return _sha1.TryComputeHash(buffer, hash, out byteCount);
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);

                var ret = _sha1.TryComputeHash(new ReadOnlySpan<byte>(buffer, 0, bufferSize), hash, out byteCount);

                ArrayPool<byte>.Shared.Return(buffer);
                return ret;
            }
        }

        public bool ComputeHash(ReadOnlySpan<byte> buffer, Span<byte> hash, out int byteCount)
        {
            return _sha1.TryComputeHash(buffer, hash, out byteCount);
        }

        public byte[]? Digest => _sha1.Hash;
        public int Length => _sha1.HashSize / 8;

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    _sha1.Dispose();
            }
            _disposed = true;
        }
    }
}
