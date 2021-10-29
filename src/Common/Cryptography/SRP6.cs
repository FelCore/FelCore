// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using static Common.Util;
using static Common.Errors;
using static Common.RandomEngine;
using Common.Extensions;

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

        static SRP6()
        {
            Span<byte> NHash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            Span<byte> gHash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];

            SHA1Hash.HashData(N, NHash, out var byteCount1);
            SHA1Hash.HashData(g, gHash, out var byteCount2);

            // NgHash = H(N) xor H(g)
            for (var i = 0; i < SHA1Hash.SHA1_DIGEST_LENGTH; i++)
                NgHash[i] = (byte)(NHash[i] ^ gHash[i]);
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

            SHA1Hash.HashData(data, result, out _);
        }

        static byte[] CalculateVerifier(string username, string password, ReadOnlySpan<byte> salt)
        {
            // v = g ^ H(s || H(u || ':' || p)) mod N

            Span<byte> data = stackalloc byte[salt.Length + SHA1Hash.SHA1_DIGEST_LENGTH];
            for (var i = 0; i < salt.Length; i++)
                data[i] = salt[i];

            SHA1Hash.HashData($"{username}:{password}", data.Slice(salt.Length), out var byteCount);

            Span<byte> hash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            SHA1Hash.HashData(data, hash, out byteCount);

            var result = new byte[32];
            _g.ModPow(new BigInteger(hash, true), _N).TryWriteBytes(result, out var _, true);

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
            _sha1.UpdateData(buf0.Slice(p, EPHEMERAL_KEY_LENGTH / 2 - p));
            _sha1.Finish();
            _sha1.GetDigest(hash0);

            _sha1.Initialize();
            _sha1.UpdateData(buf1.Slice(p, EPHEMERAL_KEY_LENGTH / 2 - p));
            _sha1.Finish();
            _sha1.GetDigest(hash1);

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
            ((_g.ModPow(b, _N) + (v * 3)) % _N).TryWriteBytes(result, out var _, true);
        }

        public readonly byte[] Salt;
        void* _ptrB;
        public ReadOnlySpan<byte> B => new ReadOnlySpan<byte>(_ptrB, 32); // B = 3v + g^b

        SHA1Hash _sha1;
        bool _used = false; // a single instance can only be used to verify once

        /* per-instantiation parameters, set on construction */
        string _username; // H(I) - the username, all uppercase
        BigInteger _b; // b - randomly chosen by the server, 19 bytes, never given out
        BigInteger _v; // v - the user's password verifier, derived from s + H(USERNAME || ":" || PASSWORD)

        bool _disposed;

        public SRP6(string username, byte[] salt, ReadOnlySpan<byte> verifier, SHA1Hash sha1)
        {
            _username = username;
            _sha1 = sha1;

            Span<byte> span = stackalloc byte[32];
            GetRandomBytes(span);
            _b = new BigInteger(span, true);
            _v = new BigInteger(verifier, true);

            Salt = salt;
            _ptrB = NativeMemory.Alloc(32);
            _B(ref _b, ref _v, new Span<byte>(_ptrB, 32));
        }

        public byte[]? VerifyChallengeResponse(ReadOnlySpan<byte> A, ReadOnlySpan<byte> clientM)
        {
            Assert(!_used, "A single SRP6 object must only ever be used to verify ONCE!");
            _used = true;

            var _A = new BigInteger(A, true);
            if ((_A % _N).IsZero)
                return null;

            // EphemeralKey A + B
            Span<byte> hash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            _sha1.Initialize();
            _sha1.UpdateData(A);
            _sha1.UpdateData(B);
            _sha1.Finish();
            _sha1.GetDigest(hash);

            var u = new BigInteger(hash, true);

            Span<byte> S = stackalloc byte[32];
            (_A * _v.ModPow(u, _N)).ModPow(_b, _N).TryWriteBytes(S, out _, true);

            var K = SHA1Interleave(S);

            Span<byte> I = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            _sha1.Initialize();
            _sha1.UpdateData(_username);
            _sha1.Finish();
            _sha1.GetDigest(I);

            _sha1.Initialize();
            // Payload = NgHash + I + Salt + A + B + K;
            _sha1.UpdateData(NgHash);
            _sha1.UpdateData(I);
            _sha1.UpdateData(Salt);
            _sha1.UpdateData(A);
            _sha1.UpdateData(B);
            _sha1.UpdateData(K);
            _sha1.Finish();
            _sha1.GetDigest(hash);

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
                if (_ptrB != default)
                {
                    NativeMemory.Free(_ptrB);
                    _ptrB = default;
                }

                _disposed = true;
            }
        }
    }
}
