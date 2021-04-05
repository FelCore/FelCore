// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Threading;

namespace Common
{
    public static class RandomEngine
    {
        private static int RandomSeedCount = 0;

        private static readonly ThreadLocal<Random> _threadLocalRandom = new ThreadLocal<Random>(() => {
            return new Random((int)(Time.Now.Ticks << 4) + Interlocked.Increment(ref RandomSeedCount));
        });

        public static Random Rand => _threadLocalRandom.Value!;

        public static byte[] GetRandomBytes(int length)
        {
            var ret = new byte[length];
            Rand.NextBytes(ret);

            return ret;
        }

        public static void GetRandomBytes(byte[] buffer)
        {
            Rand.NextBytes(buffer);
        }

        public static void GetRandomBytes(Span<byte> buffer)
        {
            Rand.NextBytes(buffer);
        }
    }
}