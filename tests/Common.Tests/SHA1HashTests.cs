// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

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
                Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(sha1.Digest));
            }
        }

        [TestMethod]
        public void ShouldHashMultipleData()
        {
            using (var sha1 = new SHA1Hash())
            {
                sha1.UpdateData("1");
                sha1.Finish();
                Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(sha1.Digest));

                sha1.Initialize();
                sha1.UpdateData("1");
                sha1.UpdateData("2");
                sha1.Finish();
                Assert.AreEqual("7B52009B64FD0A2A49E6D8A939753077792B0554", ByteArrayToHexStr(sha1.Digest));
            }
        }

        [TestMethod]
        public void ShouldHashLength20()
        {
            using (var sha1 = new SHA1Hash())
            {
                sha1.UpdateData("3");
                sha1.Finish();
                Assert.AreEqual("77DE68DAECD823BABBB58EDB1C8E14D7106E83BB", ByteArrayToHexStr(sha1.Digest));
                Assert.AreEqual(20, sha1.Length);
            }
        }
    }
}
