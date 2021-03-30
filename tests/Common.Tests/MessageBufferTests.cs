// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Common.Tests
{
    [TestClass]
    public class MessageBufferTests
    {
        private MessageBuffer _buffer;

        public MessageBufferTests()
        {
            _buffer = new MessageBuffer();
        }

        [TestMethod]
        public void ShouldResize()
        {
            var newSize = 1024;
            _buffer.Resize(newSize);

            Assert.AreEqual(newSize, _buffer.Data().Length);
        }

        [TestMethod]
        public void ShouldReadCompleted()
        {
            _buffer.WriteCompleted(10);
            var oldRpos = _buffer.Rpos();

            var bytes = 4;
            _buffer.ReadCompleted(bytes);

            Assert.AreEqual(_buffer.Rpos(), oldRpos + bytes);
        }

        [TestMethod]
        public void ShouldReadCompletedIfWPosIs0()
        {
            var bytes = 4;
            _buffer.ReadCompleted(bytes);

            Assert.AreEqual(_buffer.Rpos(), 0);
        }

        [TestMethod]
        public void ShouldWriteCompleted()
        {
            var oldWpos = _buffer.Wpos();

            var bytes = 4;
            _buffer.WriteCompleted(bytes);

            Assert.AreEqual(_buffer.Wpos(), oldWpos + bytes);
        }

        [TestMethod]
        public void ShouldEnsureFreeSpace()
        {
            _buffer.WriteCompleted(_buffer.GetBufferSize());
            Assert.IsTrue(_buffer.GetRemainingSpace() == 0);

            _buffer.EnsureFreeSpace();

            Assert.IsTrue(_buffer.GetRemainingSpace() > 0);
        }

        [TestMethod]
        public void ShouldWriteWidthWposIs0()
        {
            _buffer = new MessageBuffer();

            byte[] bytes = new byte[] { (byte)'1', (byte)'a', (byte)4 };
            _buffer.Write(bytes.AsSpan());

            Assert.AreEqual((byte)'1', _buffer.Data()[0]);
            Assert.AreEqual((byte)'a', _buffer.Data()[1]);
            Assert.AreEqual((byte)4, _buffer.Data()[2]);
        }

        [TestMethod]
        public void ShouldWriteWidthWposIs0AndSize()
        {
            _buffer = new MessageBuffer();

            byte[] bytes = new byte[] { (byte)'1', (byte)'a', (byte)4 };
            _buffer.Write(bytes.AsSpan(0, 2));

            Assert.AreEqual((byte)'1', _buffer.Data()[0]);
            Assert.AreEqual((byte)'a', _buffer.Data()[1]);
            Assert.AreNotEqual((byte)4, _buffer.Data()[2]);
        }

        [TestMethod]
        public void ShouldWriteWidthWposIsNot0()
        {
            _buffer = new MessageBuffer();
            _buffer.WriteCompleted(2);

            byte[] bytes = new byte[] { (byte)'1', (byte)'a', (byte)4 };
            _buffer.Write(bytes.AsSpan());

            Assert.AreEqual((byte)'1', _buffer.Data()[2]);
            Assert.AreEqual((byte)'a', _buffer.Data()[3]);
            Assert.AreEqual((byte)4, _buffer.Data()[4]);
        }

        [TestMethod]
        public void ShouldNormalize()
        {
            _buffer = new MessageBuffer();
            byte[] bytes = new byte[] { (byte)'1', (byte)'a', (byte)4, (byte)'R', (byte)'y', (byte)'a', (byte)'n' };
            _buffer.Write(bytes.AsSpan());

            _buffer.ReadCompleted(2);

            _buffer.Normalize();

            Assert.AreEqual(0, _buffer.Rpos());
            Assert.AreEqual(7 - 2, _buffer.Wpos());
            Assert.AreEqual((byte)4, _buffer.Data()[0]);
            Assert.AreEqual((byte)'R', _buffer.Data()[1]);
            Assert.AreEqual((byte)'y', _buffer.Data()[2]);
            Assert.AreEqual((byte)'a', _buffer.Data()[3]);
            Assert.AreEqual((byte)'n', _buffer.Data()[4]);
        }
    }
}
