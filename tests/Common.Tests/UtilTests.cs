// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Common.Util;

namespace Common.Tests
{
    [TestClass]
    public class UtilTests
    {
        [TestMethod]
        public void ShouldByteArrayToHexStr()
        {
            Assert.AreEqual("FFFF", ByteArrayToHexStr(new byte[] { 255, 255 }));
            Assert.AreEqual("00FF", ByteArrayToHexStr(new byte[] { 0, 255 }));
            Assert.AreEqual("000B", ByteArrayToHexStr(new byte[] { 0, 11 }));
        }

        [TestMethod]
        public void ShouldByteArrayToHexStrReverse()
        {
            Assert.AreEqual("FFFF", ByteArrayToHexStr(new byte[] { 255, 255 }, true));
            Assert.AreEqual("FF00", ByteArrayToHexStr(new byte[] { 0, 255 }, true));
            Assert.AreEqual("0B00", ByteArrayToHexStr(new byte[] { 0, 11 }, true));
        }

        [TestMethod]
        public void ShouldHexStrToByteArray()
        {
            CollectionAssert.AreEqual(new byte[] { 255, 255 }, HexStrToByteArray("FFFF"));
            CollectionAssert.AreEqual(new byte[] { 0, 255 }, HexStrToByteArray("00FF"));
            CollectionAssert.AreEqual(new byte[] { 0, 11 }, HexStrToByteArray("000B"));
        }

        [TestMethod]
        public void ShouldHexStrToByteArrayReverse()
        {
            CollectionAssert.AreEqual(new byte[] { 255, 255 }, HexStrToByteArray("FFFF", true));
            CollectionAssert.AreEqual(new byte[] { 255, 0 }, HexStrToByteArray("00FF", true));
            CollectionAssert.AreEqual(new byte[] { 11, 0 }, HexStrToByteArray("000B", true));
        }

        [TestMethod]
        public void ShouldDigestSHA1ForStr()
        {
            Assert.AreEqual("356A192B7913B04C54574D18C28D46E6395428AB", ByteArrayToHexStr(DigestSHA1("1")));
        }

        [TestMethod]
        public void ShouldIPv4InNetwork()
        {
            IPAddress network = IPAddress.Parse("172.20.76.0");
            IPAddress subnetMask = IPAddress.Parse("255.255.254.0");

            IPAddress address = IPAddress.Parse("172.20.76.5");
            Assert.IsTrue(IPv4InNetwork(address, subnetMask, network));

            address = IPAddress.Parse("172.20.77.5");
            Assert.IsTrue(IPv4InNetwork(address, subnetMask, network));

            address = IPAddress.Parse("172.20.78.5");
            Assert.IsFalse(IPv4InNetwork(address, subnetMask, network));
        }

        [TestMethod]
        public void ShouldIPv4InNetworkWithNetworkAddresIsIPAddress()
        {
            IPAddress network = IPAddress.Parse("172.20.76.1");
            IPAddress subnetMask = IPAddress.Parse("255.255.254.0");

            IPAddress address = IPAddress.Parse("172.20.76.5");
            Assert.IsTrue(IPv4InNetwork(address, subnetMask, network));
        }

        [TestMethod]
        public void ShouldResolveIPAddressForDomain()
        {
            var address = Util.ResolveIPAddress("bing.com");
            Assert.IsTrue(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        }

        [TestMethod]
        public void ShouldNotResolveIPAddressForInvalidDomain()
        {
            var address = Util.ResolveIPAddress("abcabc");
            Assert.IsNull(address);
        }

        [TestMethod]
        public void ShouldResolveIPAddressForIP()
        {
            var address = Util.ResolveIPAddress("127.0.0.1");
            Assert.IsTrue(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            address = Util.ResolveIPAddress("8.8.8.8");
            Assert.IsTrue(address.ToString().StartsWith("8.8."));
        }

        [TestMethod]
        public void ShouldNotResolveIPAddressForInvalidIP()
        {
            var address = Util.ResolveIPAddress("192.168.1111.1");
            Assert.IsNull(address);
        }

        [TestMethod]
        public void ShouldResolveSubnetMaskAddress()
        {
            var address = Util.ResolveIPAddress("255.255.255.0");
            Assert.IsNotNull(address);
        }
    }
}
