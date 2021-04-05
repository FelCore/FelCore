// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Text;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Server.Shared.Tests
{
    [TestClass]
    public class ByteBufferTests
    {
        private ByteBuffer _buffer;

        public ByteBufferTests()
        {
            _buffer = new ByteBuffer();
        }

        [TestMethod]
        public void ShouldResize()
        {
            var newSize = 1024;
            _buffer.Resize(newSize);

            Assert.AreEqual(newSize, _buffer.Data().Length);
        }

        [TestMethod]
        public void ShouldAppendByte()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual((byte)' ', _buffer[1]);
        }

        [TestMethod]
        public void ShouldAppendBool()
        {
            _buffer = new ByteBuffer();

            _buffer.Append(true);
            _buffer.Append(false);

            Assert.AreEqual(1, _buffer[0]);
            Assert.AreEqual(0, _buffer[1]);
        }

        [TestMethod]
        public void ShouldAppendUshort()
        {
            _buffer = new ByteBuffer(10);

            _buffer.Append((ushort)'你');
            _buffer.Append((ushort)'好');

            byte[] bytes1 = new byte[2] { _buffer.Data()[0], _buffer.Data()[1] };
            byte[] bytes2 = new byte[2] { _buffer.Data()[2], _buffer.Data()[3] };

            Assert.AreEqual((ushort)'你', BitConverter.ToUInt16(bytes1, 0));
            Assert.AreEqual((ushort)'好', BitConverter.ToUInt16(bytes2, 0));
        }

        [TestMethod]
        public void ShouldAppendShort()
        {
            _buffer = new ByteBuffer(10);

            _buffer.Append((short)'你');
            _buffer.Append((short)'好');

            byte[] bytes1 = new byte[2] { _buffer.Data()[0], _buffer.Data()[1] };
            byte[] bytes2 = new byte[2] { _buffer.Data()[2], _buffer.Data()[3] };

            Assert.AreEqual((short)'你', BitConverter.ToInt16(bytes1, 0));
            Assert.AreEqual((short)'好', BitConverter.ToInt16(bytes2, 0));
        }

        [TestMethod]
        public void ShouldAppendUInt()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((uint)'你');
            _buffer.Append((uint)'好');

            byte[] bytes1 = new byte[4] { _buffer.Data()[0], _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3] };
            byte[] bytes2 = new byte[4] { _buffer.Data()[4], _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7] };

            Assert.AreEqual((uint)'你', BitConverter.ToUInt32(bytes1, 0));
            Assert.AreEqual((uint)'好', BitConverter.ToUInt32(bytes2, 0));
        }

        [TestMethod]
        public void ShouldAppendULong()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((ulong)'你');
            _buffer.Append((ulong)'好');

            byte[] bytes1 = new byte[8]
            {
                _buffer.Data()[0], _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3],
                _buffer.Data()[4], _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7],
            };

            byte[] bytes2 = new byte[8]
            {
                _buffer.Data()[8], _buffer.Data()[9], _buffer.Data()[10], _buffer.Data()[11],
                _buffer.Data()[12], _buffer.Data()[13], _buffer.Data()[14], _buffer.Data()[15],
            };

            Assert.AreEqual((ulong)'你', BitConverter.ToUInt64(bytes1, 0));
            Assert.AreEqual((ulong)'好', BitConverter.ToUInt64(bytes2, 0));
        }

        [TestMethod]
        public void ShouldAppendFloat()
        {
            _buffer = new ByteBuffer();

            float f1 = 1000001.002f;
            float f2 = 1000002.0002f;

            _buffer.Append(f1);
            _buffer.Append(f2);

            byte[] bytes1 = new byte[4] { _buffer.Data()[0], _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3] };
            byte[] bytes2 = new byte[4] { _buffer.Data()[4], _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7] };

            Assert.AreEqual(f1, BitConverter.ToSingle(bytes1, 0));
            Assert.AreEqual(f2, BitConverter.ToSingle(bytes2, 0));
        }

        [TestMethod]
        public void ShouldAppendDouble()
        {
            _buffer = new ByteBuffer();

            double d1 = 1000001.002;
            double d2 = 1000002.0002;

            _buffer.Append(d1);
            _buffer.Append(d2);

            byte[] bytes1 = new byte[8]
            {
                _buffer.Data()[0], _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3],
                _buffer.Data()[4], _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7],
            };

            byte[] bytes2 = new byte[8]
            {
                _buffer.Data()[8], _buffer.Data()[9], _buffer.Data()[10], _buffer.Data()[11],
                _buffer.Data()[12], _buffer.Data()[13], _buffer.Data()[14], _buffer.Data()[15],
            };

            Assert.AreEqual(d1, BitConverter.ToDouble(bytes1, 0));
            Assert.AreEqual(d2, BitConverter.ToDouble(bytes2, 0));
        }

        [TestMethod]
        public void ShouldAppendASCIIString()
        {
            _buffer = new ByteBuffer();

            var str = "Hello!01";

            _buffer.Append(str);

            byte[] bytes1 = new byte[8]
            {
                _buffer.Data()[0], _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3],
                _buffer.Data()[4], _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7],
            };

            Assert.AreEqual(str, Encoding.UTF8.GetString(bytes1));
        }

        [TestMethod]
        public void ShouldAppendUnicodeString()
        {
            _buffer = new ByteBuffer();

            var str = "你好0!";

            _buffer.Append(str);

            byte[] bytes1 = new byte[8]
            {
                _buffer.Data()[0], _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3],
                _buffer.Data()[4], _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7],
            };

            Assert.AreEqual(str, Encoding.UTF8.GetString(bytes1));
        }

        [TestMethod]
        public void ShouldFinishRead()
        {
            _buffer = new ByteBuffer();
            _buffer.Append(new byte[] { (byte)'a', (byte)'1', 100 });

            _buffer.FinishRead();

            Assert.AreEqual(3, _buffer.Rpos());
        }

        [TestMethod]
        public void ShouldClear()
        {
            _buffer = new ByteBuffer();
            _buffer.Append(new byte[] { (byte)'a', (byte)'1', 100 });

            _buffer.Clear();

            Assert.AreEqual(0, _buffer.Rpos());
            Assert.AreEqual(0, _buffer.Wpos());
        }

        [TestMethod]
        public void ShouldPutByte()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, (byte)'b');

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual((byte)'b', _buffer[1]);
        }

        [TestMethod]
        public void ShouldPutBool()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, false);

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual((byte)0, _buffer[1]);
        }

        [TestMethod]
        public void ShouldPutUShort()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, (ushort)'文');

            byte[] bytes1 = new byte[2] { _buffer.Data()[1], _buffer.Data()[2] };

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual((ushort)'文', BitConverter.ToUInt16(bytes1, 0));
        }

        [TestMethod]
        public void ShouldPutInt()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, (int)'文');

            byte[] bytes1 = new byte[4] { _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3], _buffer.Data()[4] };

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual((int)'文', BitConverter.ToInt32(bytes1, 0));
        }

        [TestMethod]
        public void ShouldPutULong()
        {
            _buffer = new ByteBuffer();

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, (ulong)'文');

            byte[] bytes1 = new byte[8] {
                _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3], _buffer.Data()[4],
                _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7], _buffer.Data()[8]
            };

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual((ulong)'文', BitConverter.ToUInt64(bytes1, 0));
        }

        [TestMethod]
        public void ShouldPutFloat()
        {
            _buffer = new ByteBuffer();
            var f1 = 1000001.001f;

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, f1);

            byte[] bytes1 = new byte[4] { _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3], _buffer.Data()[4] };

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual(f1, BitConverter.ToSingle(bytes1, 0));
        }

        [TestMethod]
        public void ShouldPutDouble()
        {
            _buffer = new ByteBuffer();
            var d1 = 100000232424.02032341;

            _buffer.Append((byte)'a');
            _buffer.Append((byte)' ');
            _buffer.Put(1, d1);

            byte[] bytes1 = new byte[8] {
                _buffer.Data()[1], _buffer.Data()[2], _buffer.Data()[3], _buffer.Data()[4],
                _buffer.Data()[5], _buffer.Data()[6], _buffer.Data()[7], _buffer.Data()[8]
            };

            Assert.AreEqual((byte)'a', _buffer[0]);
            Assert.AreEqual(d1, BitConverter.ToDouble(bytes1, 0));
        }

        [TestMethod]
        public void ShouldReadBool()
        {
            _buffer = new ByteBuffer();
            _buffer.Append(true);
            _buffer.Append(false);

            Assert.IsTrue(_buffer.Read<bool>());
            Assert.IsFalse(_buffer.Read<bool>());
        }

        [TestMethod]
        public void ShouldReadByte()
        {
            _buffer = new ByteBuffer();
            _buffer.Append((byte)111);
            _buffer.Append((byte)64);

            Assert.AreEqual((byte)111, _buffer.Read<byte>());
            Assert.AreEqual((byte)64, _buffer.Read<byte>());
        }

        [TestMethod]
        public void ShouldReadSByte()
        {
            _buffer = new ByteBuffer();
            _buffer.Append((sbyte)111);
            _buffer.Append((sbyte)-64);

            Assert.AreEqual((sbyte)111, _buffer.Read<sbyte>());
            Assert.AreEqual((sbyte)-64, _buffer.Read<sbyte>());
        }

        [TestMethod]
        public void ShouldReadShort()
        {
            _buffer = new ByteBuffer();
            _buffer.Append((short)'文');
            _buffer.Append((short)'A');

            Assert.AreEqual((short)'文', _buffer.Read<short>());
            Assert.AreEqual((short)'A', _buffer.Read<short>());
        }

        [TestMethod]
        public void ShouldReadUInt()
        {
            _buffer = new ByteBuffer();
            _buffer.Append((uint)'文');
            _buffer.Append((uint)'A');

            Assert.AreEqual((uint)'文', _buffer.Read<uint>());
            Assert.AreEqual((uint)'A', _buffer.Read<uint>());
        }

        [TestMethod]
        public void ShouldReadLong()
        {
            _buffer = new ByteBuffer();
            _buffer.Append(-9223372036854775808);
            _buffer.Append(9223372036854775807);

            Assert.AreEqual(-9223372036854775808, _buffer.Read<long>());
            Assert.AreEqual(9223372036854775807, _buffer.Read<long>());
        }

        [TestMethod]
        public void ShouldReadFloat()
        {
            _buffer = new ByteBuffer();
            var f1 = 432432423.00234f;
            _buffer.Append(f1);
            _buffer.Append(9223372036854775807);

            Assert.AreEqual(f1, _buffer.ReadFloat());
        }

        [TestMethod]
        public void ShouldReadDouble()
        {
            _buffer = new ByteBuffer();
            var d1 = 9234432432423.00234;
            _buffer.Append(d1);
            _buffer.Append(9223372036854775807);

            Assert.AreEqual(d1, _buffer.ReadDouble());
        }

        [TestMethod]
        public void ShouldReadString()
        {
            _buffer = new ByteBuffer();
            var str = "Hello world 你好世界!";
            _buffer.Append(str);
            _buffer.Append(9223372036854775807);

            Assert.AreEqual(str, _buffer.ReadString());
            Assert.AreEqual(9223372036854775807, _buffer.Read<long>());
        }

        [TestMethod]
        public void ShouldReadSkip()
        {
            _buffer = new ByteBuffer();
            _buffer.Append((byte)1);
            _buffer.Append((short)2);
            _buffer.Append(9223372036854775807);

            _buffer.ReadSkip(1);

            Assert.AreEqual((short)2, _buffer.Read<short>());
            Assert.AreEqual(9223372036854775807, _buffer.Read<long>());
        }

        [TestMethod]
        public void ShouldReadSkipT()
        {
            _buffer = new ByteBuffer();
            _buffer.Append((byte)1);
            _buffer.Append((short)2);
            _buffer.Append(9223372036854775807);

            _buffer.ReadSkip<byte>();
            _buffer.ReadSkip<short>();

            Assert.AreEqual(9223372036854775807, _buffer.Read<long>());
        }

        [TestMethod]
        public void ShouldReadSkipString()
        {
            _buffer = new ByteBuffer();
            var str = "Hello world 你好世界!";
            _buffer.Append(str);
            _buffer.Append(9223372036854775807);

            _buffer.ReadSkipString();

            Assert.AreEqual(9223372036854775807, _buffer.Read<long>());
        }
    }
}
