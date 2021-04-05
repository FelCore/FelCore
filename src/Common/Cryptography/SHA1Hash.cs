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
        public const int SHA1_DIGEST_LENGTH = 20;

        private SHA1 _sha1;
        private MessageBuffer? _dataBuffer;

        private bool _disposed;

        public SHA1Hash()
        {
            _sha1 = SHA1.Create();
        }

        ~SHA1Hash()
        {
            Dispose(false);
        }

        private void EnsureAndResetDataBuffer()
        {
            if (_dataBuffer == null)
                _dataBuffer = new();

            _dataBuffer.Reset();
        }

        public void UpdateData(params BigInteger[] bigIntegers)
        {
            EnsureAndResetDataBuffer();

            int totalBytesCount = 0;
            foreach(var bn in bigIntegers)
                totalBytesCount += bn.GetByteCount(true);

            if (_dataBuffer!.GetBufferSize() < totalBytesCount)
                _dataBuffer.Resize(totalBytesCount);

            int bytesWritten = 0;
            foreach(var bn in bigIntegers)
            {
                bn.TryWriteBytes(_dataBuffer.WriteSpan, out bytesWritten, true);
                _dataBuffer.WriteCompleted(bytesWritten);
            }

            _sha1.TransformBlock(_dataBuffer.Data(), 0, _dataBuffer.Wpos(), null, 0);
        }

        public void UpdateData(ReadOnlySpan<byte> data)
        {
            EnsureAndResetDataBuffer();

            if (_dataBuffer!.GetBufferSize() < data.Length)
                _dataBuffer.Resize(data.Length);

            _dataBuffer.Write(data);

            _sha1.TransformBlock(_dataBuffer.Data(), 0, _dataBuffer.Wpos(), null, 0);
        }

        public void UpdateData(string str)
        {
            if (string.IsNullOrEmpty(str))
                return;

            EnsureAndResetDataBuffer();

            var bytesCount = Encoding.UTF8.GetByteCount(str);

            if (_dataBuffer!.GetBufferSize() < bytesCount)
                _dataBuffer.Resize(bytesCount);

            Encoding.UTF8.GetBytes(str, _dataBuffer.WriteSpan);
            _dataBuffer.WriteCompleted(bytesCount);

            _sha1.TransformBlock(_dataBuffer.Data(), 0, _dataBuffer.Wpos(), null, 0);
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
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _sha1.Dispose();
                    if (_dataBuffer != null)
                    {
                        _dataBuffer.Dispose();
                        _dataBuffer = null;
                    }
                }
            }
            _disposed = true;
        }
    }
}
