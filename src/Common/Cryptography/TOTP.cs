// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using System.Security.Cryptography;

namespace Common
{
    public static class TOTP
    {
        public static unsafe int Base32Decode(string encoded, out byte[] result)
        {
            var bytesCount = Encoding.UTF8.GetByteCount(encoded);
            int bufferSize = (bytesCount + 7)/8*5;
            result = new byte[bufferSize];

            return Base32Decode(encoded, result);
        }

        public static unsafe int Base32Decode(string encoded, Span<byte> result)
        {
            if (string.IsNullOrEmpty(encoded))
                return -1;

            int buffer = 0;
            int bitsLeft = 0;
            int count = 0;

            var bytesCount = Encoding.UTF8.GetByteCount(encoded);
            Span<byte> utf8Data = bytesCount <= Util.MaxStackLimit ? stackalloc byte[bytesCount] : new byte[bytesCount];
            Encoding.UTF8.GetBytes(encoded, utf8Data);

            int bufferSize = (bytesCount + 7)/8*5;

            Span<byte> span = result.Slice(0, bufferSize);

            for (int i = 0; i < utf8Data.Length && count < bufferSize; ++i)
            {
                byte ch = utf8Data[i];
                if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' || ch == '-')
                    continue;
                buffer <<= 5;

                // Deal with commonly mistyped characters
                if (ch == '0')
                    ch = (byte)'O';
                else if (ch == '1')
                    ch = (byte)'L';
                else if (ch == '8')
                    ch = (byte)'B';

                // Look up one base32 digit
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
                    ch = (byte)((ch & 0x1F) - 1);
                else if (ch >= '2' && ch <= '7')
                    ch -= '2' - 26;
                else
                    return -1;

                buffer |= ch;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    span[count++] = (byte)(buffer >> (bitsLeft - 8));
                    bitsLeft -= 8;
                }
            }

            if (count < bufferSize)
                span[count] = (byte)'\0';

            return count;
        }

        public const int HMAC_RES_SIZE = 20;

        public static int GenerateToken(string base32Key)
        {
            var keySize = Encoding.UTF8.GetByteCount(base32Key);

            int bufferSize = (keySize + 7)/8*5;

            byte[] decoded = new byte[bufferSize];

            int hmacResSize = HMAC_RES_SIZE;
            Span<byte> hmacRes = stackalloc byte[HMAC_RES_SIZE];
            long timestamp = Time.GetTimestamp(Time.Now)/30;

            Span<byte> challenge = stackalloc byte[8];

            for (int i = 7; i >= 0; i--, timestamp >>= 8)
                challenge[i] = (byte)timestamp;

            Base32Decode(base32Key, decoded);

            var hmac = new HMACSHA1(decoded);
            hmac.TryComputeHash(challenge, hmacRes, out hmacResSize);
            hmac.Dispose();

            int offset = hmacRes[19] & 0xF;
            int truncHash = (hmacRes[offset] << 24) | (hmacRes[offset+1] << 16 )| (hmacRes[offset+2] << 8) | (hmacRes[offset+3]);
            truncHash &= 0x7FFFFFFF;

            return truncHash % 1000000;
        }
    }
}
