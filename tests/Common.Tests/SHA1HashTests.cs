// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Common.Util;

namespace Common.Tests
{
    [TestClass]
    public class SHA1HashTests
    {
        [TestMethod]
        public void ShouldHashSingleData()
        {
            using (var sha1 = new SHA1Hash())
            {
                sha1.UpdateData("1");
                sha1.Finish();
                Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(sha1.GetDigest()));
            }
        }

        [TestMethod]
        public void ShouldHashMultipleData()
        {
            using (var sha1 = new SHA1Hash())
            {
                sha1.UpdateData("1");
                sha1.Finish();
                Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(sha1.GetDigest()));

                sha1.Initialize();
                sha1.UpdateData("1");
                sha1.UpdateData("2");
                sha1.Finish();
                Assert.AreEqual("7B52009B64FD0A2A49E6D8A939753077792B0554", ByteArrayToHexStr(sha1.GetDigest()));
            }
        }

        [TestMethod]
        public void ShouldHashLength20()
        {
            using (var sha1 = new SHA1Hash())
            {
                sha1.UpdateData("3");
                sha1.Finish();
                Assert.AreEqual("77DE68DAECD823BABBB58EDB1C8E14D7106E83BB", ByteArrayToHexStr(sha1.GetDigest()));
                Assert.AreEqual(20, sha1.GetDigest().Length);
            }
        }

        [TestMethod]
        public void ShouldHashReadOnlySpan()
        {
            using (var sha1 = new SHA1Hash())
            {
                var data = new byte[1] { 49 };
                sha1.UpdateData(data);
                sha1.Finish();
                Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(sha1.GetDigest()));

                sha1.Initialize();
                data = new byte[2] { 49, 50 };
                sha1.UpdateData(data);
                sha1.Finish();
                Assert.AreEqual("7B52009B64FD0A2A49E6D8A939753077792B0554", ByteArrayToHexStr(sha1.GetDigest()));
            }
        }

        [TestMethod]
        public void ShouldHashBigIntegers()
        {
            using (var sha1 = new SHA1Hash())
            {
                var data = new byte[1] { 49 };
                BigInteger bn1 = new(data, true);
                sha1.UpdateData(bn1);
                sha1.Finish();
                Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(sha1.GetDigest()));

                data = new byte[2] { 49, 50 };
                bn1 = new(data, true);
                sha1.Initialize();
                sha1.UpdateData(bn1);
                sha1.Finish();
                Assert.AreEqual("7B52009B64FD0A2A49E6D8A939753077792B0554", ByteArrayToHexStr(sha1.GetDigest()));
            }
        }

        [TestMethod]
        public void ShouldHashAsStandardSha1()
        {
            using (var sha1 = new SHA1Hash())
            {
                var data1 = new byte[1] { 49 };
                var data2 = new byte[2] { 49, 50 };

                sha1.UpdateData(data1);
                sha1.UpdateData(data2);
                sha1.Finish();

                var stdSha1 = System.Security.Cryptography.SHA1.Create();
                stdSha1.TransformBlock(data1, 0, 1, null, 0);
                stdSha1.TransformBlock(data2, 0, 2, null, 0);
                stdSha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                CollectionAssert.AreEqual(sha1.GetDigest(), stdSha1.Hash);
            }
        }
    }
}
