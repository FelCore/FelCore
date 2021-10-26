// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;

namespace Common
{
    public static class RandomEngine
    {
        public static byte[] GetRandomBytes(int length)
        {
            var ret = new byte[length];
            Random.Shared.NextBytes(ret);

            return ret;
        }

        public static void GetRandomBytes(byte[] buffer)
        {
            Random.Shared.NextBytes(buffer);
        }

        public static void GetRandomBytes(Span<byte> buffer)
        {
            Random.Shared.NextBytes(buffer);
        }
    }
}
