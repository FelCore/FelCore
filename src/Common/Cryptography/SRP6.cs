// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Buffers;
using System.Numerics;
using static Common.Util;
using static Common.Errors;
using static Common.RandomEngine;

namespace Common
{
    public unsafe sealed class SRP6 : IDisposable
    {
        public const int SALT_LENGTH = 32;
        public const int VERIFIER_LENGTH = 32;
        public const int EPHEMERAL_KEY_LENGTH = 32;
        public const int SESSION_KEY_LENGTH = 40;

        public static readonly byte[] g = new byte[] { 7 };
        public static readonly byte[] N = HexStrToByteArray("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7", true);
        public static readonly byte[] NgHash = new byte[20];

        static ArrayPool<byte> BufferPool = ArrayPool<byte>.Create();

        static SRP6()
        {
            Span<byte> NHash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            Span<byte> gHash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];

            using (var sha1 = new SHA1Hash())
            {
                sha1.ComputeHash(N, NHash, out var byteCount1);
                sha1.Initialize();
                sha1.ComputeHash(g, gHash, out var byteCount2);

                // NgHash = H(N) xor H(g)
                for (var i = 0; i < SHA1Hash.SHA1_DIGEST_LENGTH; i++)
                    NgHash[i] = (byte)(NHash[i] ^ gHash[i]);
            }
        }

        /// <summary>
        /// username + password must be converted to upcase FIRST!
        /// </summary>
        public static (byte[], byte[]) MakeRegistrationData(string username, string password)
        {
            (byte[], byte[]) res;
            res.Item1 = new byte[32];

            GetRandomBytes(res.Item1); // random salt
            res.Item2 = CalculateVerifier(username, password, res.Item1);
            return res;
        }

        /// <summary>
        /// username + password must be converted to upcase FIRST!
        /// </summary>
        public static bool CheckLogin(string username, string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> verifier)
        {
            return verifier.SequenceEqual(CalculateVerifier(username, password, salt));
        }

        public static void GetSessionVerifier(ReadOnlySpan<byte> A, ReadOnlySpan<byte> clientM, ReadOnlySpan<byte> K, Span<byte> result)
        {
            // A len 32
            // clientM len 20
            // K len 40
            Span<byte> data = stackalloc byte[32 + 20 + 40];
            A.CopyTo(data);
            clientM.CopyTo(data.Slice(32));
            K.CopyTo(data.Slice(52));

            var sha = new SHA1Hash();
            sha.ComputeHash(data, result, out _);
            sha.Dispose();
        }

        static byte[] CalculateVerifier(string username, string password, ReadOnlySpan<byte> salt)
        {
            // v = g ^ H(s || H(u || ':' || p)) mod N

            Span<byte> data = stackalloc byte[salt.Length + SHA1Hash.SHA1_DIGEST_LENGTH];
            for (var i = 0; i < salt.Length; i++)
                data[i] = salt[i];

            var sha = new SHA1Hash();
            sha.ComputeHash($"{username}:{password}", data.Slice(salt.Length), out var byteCount);

            sha.Initialize();

            Span<byte> hash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            sha.ComputeHash(data, hash, out byteCount);

            var result = new byte[32];
            BigInteger.ModPow(_g, new BigInteger(hash, true), _N).TryWriteBytes(result, out var _, true);

            return result;
        }
        byte[] SHA1Interleave(ReadOnlySpan<byte> S)
        {
            // split S into two buffers
            Span<byte> buf0 = stackalloc byte[EPHEMERAL_KEY_LENGTH / 2];
            Span<byte> buf1 = stackalloc byte[EPHEMERAL_KEY_LENGTH / 2];
            for (int i = 0; i < EPHEMERAL_KEY_LENGTH / 2; ++i)
            {
                buf0[i] = S[2 * i + 0];
                buf1[i] = S[2 * i + 1];
            }

            // find position of first nonzero byte
            int p = 0;
            while (p < EPHEMERAL_KEY_LENGTH && S[p] == 0) ++p;
            if ((p & 1) == 1) ++p; // skip one extra byte if p is odd
            p /= 2; // offset into buffers

            // hash each of the halves, starting at the first nonzero byte
            Span<byte> hash0 = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            Span<byte> hash1 = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];

            _sha1.Initialize();
            _sha1.ComputeHash(buf0.Slice(p, EPHEMERAL_KEY_LENGTH / 2 - p), hash0, out var byteCount1);
            _sha1.Initialize();
            _sha1.ComputeHash(buf1.Slice(p, EPHEMERAL_KEY_LENGTH / 2 - p), hash1, out var byteCount2);

            // stick the two hashes back together
            byte[] K = new byte[SESSION_KEY_LENGTH];

            for (var i = 0; i < SHA1Hash.SHA1_DIGEST_LENGTH; ++i)
            {
                K[2 * i + 0] = hash0[i];
                K[2 * i + 1] = hash1[i];
            }
            return K;
        }
        static BigInteger _g = new BigInteger(g, true); // a [g]enerator for the ring of integers mod N, algorithm parameter
        static BigInteger _N = new BigInteger(N, true); // the modulus, an algorithm parameter; all operations are mod this
        static void _B(ref BigInteger b, ref BigInteger v, Span<byte> result)
        {
            ((BigInteger.ModPow(_g, b, _N) + (v * 3)) % _N).TryWriteBytes(result, out var _, true);
        }

        public readonly byte[] Salt;
        public readonly byte[] B; // B = 3v + g^b

        SHA1Hash _sha1;
        bool _used = false; // a single instance can only be used to verify once

        /* per-instantiation parameters, set on construction */
        string _username; // H(I) - the username, all uppercase
        BigInteger _b; // b - randomly chosen by the server, 19 bytes, never given out
        BigInteger _v; // v - the user's password verifier, derived from s + H(USERNAME || ":" || PASSWORD)

        const int MDATA_LENGTH = 20 + 20 + 32 + 32 + 32 + 40;

        bool _disposed;

        public SRP6(string username, byte[] salt, ReadOnlySpan<byte> verifier)
        {
            _sha1 = new SHA1Hash();
            _username = username;

            Span<byte> span = stackalloc byte[32];
            GetRandomBytes(span);
            _b = new BigInteger(span, true);
            _v = new BigInteger(verifier, true);

            Salt = salt;
            B = BufferPool.Rent(32);
            _B(ref _b, ref _v, B);
        }

        public byte[]? VerifyChallengeResponse(ReadOnlySpan<byte> A, ReadOnlySpan<byte> clientM)
        {
            Assert(!_used, "A single SRP6 object must only ever be used to verify ONCE!");
            _used = true;

            var _A = new BigInteger(A, true);
            if ((_A % _N).IsZero)
                return null;

            // EphemeralKey A + B
            Span<byte> dataAB = stackalloc byte[EPHEMERAL_KEY_LENGTH * 2];
            for (var i = 0; i < EPHEMERAL_KEY_LENGTH; i++)
                dataAB[i] = A[i];
            for (var i = 32; i < EPHEMERAL_KEY_LENGTH * 2; i++)
                dataAB[i] = B[i % 32];

            _sha1.Initialize();

            Span<byte> hash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            _sha1.ComputeHash(dataAB, hash, out _);
            var u = new BigInteger(hash, true);

            Span<byte> S = stackalloc byte[32];
            BigInteger.ModPow(_A * BigInteger.ModPow(_v, u, _N), _b, _N).TryWriteBytes(S, out _, true);

            var K = SHA1Interleave(S);

            Span<byte> I = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            _sha1.Initialize();
            _sha1.ComputeHash(_username, I, out _);

            // MData = NgHash + I + Salt + A + B + K;
            Span<byte> MData = stackalloc byte[MDATA_LENGTH];
            NgHash.AsSpan().CopyTo(MData);
            I.CopyTo(MData.Slice(20));
            Salt.AsSpan().CopyTo(MData.Slice(40));
            A.CopyTo(MData.Slice(72));
            B.AsSpan().CopyTo(MData.Slice(104));
            K.AsSpan().CopyTo(MData.Slice(136));

            _sha1.Initialize();
            _sha1.ComputeHash(MData, hash, out _);

            if (clientM.SequenceEqual(hash))
                return K;
            else
                return null;
        }

        ~SRP6()
        {
            Dispose(false);
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
                _sha1?.Dispose();
                BufferPool.Return(B, true);

                _disposed = true;
            }
        }
    }
}
