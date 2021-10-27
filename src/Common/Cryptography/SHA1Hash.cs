// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Common
{
    public unsafe sealed class SHA1Hash : IDisposable
    {
        public const int SHA1_DIGEST_LENGTH = 20;

        public static bool HashData(string str, Span<byte> destination)
        {
            return HashData(str, destination, out var _);
        }

        public static bool HashData(string str, Span<byte> destination, out int byteCount)
        {
            var count = Encoding.UTF8.GetByteCount(str);
            void* buff = NativeMemory.Alloc((uint)count);

            Span<byte> dataSpan = new Span<byte>(buff, count);
            Encoding.UTF8.GetBytes(str, dataSpan);

            try
            {
                return HashData(dataSpan, destination, out byteCount);
            }
            finally
            {
                NativeMemory.Free(buff);
            }
        }

        public static bool HashData(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            return HashData(source, destination, out var _);
        }

        public static bool HashData(ReadOnlySpan<byte> source, Span<byte> destination, out int byteCount)
        {
            return SHA1.TryHashData(source, destination, out byteCount);
        }

        MessageBufferHG _dataBuffer;
        void* _digest;
        bool _hasDigest;

        bool _disposed;

        public SHA1Hash()
        {
            _dataBuffer = new();
            _digest = NativeMemory.Alloc((uint)SHA1_DIGEST_LENGTH);
        }

        ~SHA1Hash()
        {
            Dispose(false);
        }

        public byte[]? GetDigest()
        {
            if (!_hasDigest)
                return null;

            var ret = new byte[SHA1_DIGEST_LENGTH];
            GetDigest(ret);

            return ret;
        }

        public bool GetDigest(Span<byte> destination)
        {
            if (!_hasDigest)
                return false;

            new Span<byte>(_digest, SHA1_DIGEST_LENGTH).CopyTo(destination);
            return true;
        }

        private void EnsureDataBufferHasSpace(int addSize)
        {
            if (_dataBuffer.GetRemainingSpace() < addSize)
            {
                var diff = addSize - _dataBuffer.GetRemainingSpace();
                _dataBuffer.Resize(_dataBuffer.GetBufferSize() + diff);
            }
        }

        public void UpdateData(BigInteger bigNum)
        {
            int totalBytesCount = bigNum.GetByteCount(true);

            EnsureDataBufferHasSpace(totalBytesCount);

            int bytesWritten = 0;
            bigNum.TryWriteBytes(_dataBuffer.WriteSpan, out bytesWritten, true);
            _dataBuffer.WriteCompleted(bytesWritten);
        }

        public void UpdateData(ReadOnlySpan<byte> data)
        {
            EnsureDataBufferHasSpace(data.Length);
            _dataBuffer.Write(data);
        }

        public void UpdateData(string str)
        {
            if (string.IsNullOrEmpty(str))
                return;

            var bytesCount = Encoding.UTF8.GetByteCount(str);
            EnsureDataBufferHasSpace(bytesCount);

            Encoding.UTF8.GetBytes(str, _dataBuffer.WriteSpan);
            _dataBuffer.WriteCompleted(bytesCount);
        }

        public void Initialize()
        {
            _hasDigest = false;
            _dataBuffer.Reset();
        }

        public void Finish()
        {
            SHA1.HashData(_dataBuffer.GetReadSpan(), new Span<byte>(_digest, SHA1_DIGEST_LENGTH));
            _hasDigest = true;
        }

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
                    _dataBuffer.Dispose();

                if (_digest != default)
                {
                    NativeMemory.Free(_digest);
                    _digest = default;
                }
            }
            _disposed = true;
        }
    }
}
