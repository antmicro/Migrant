/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using NUnit.Framework;
using System.IO;
using System;
using System.Linq;

namespace Migrantoid.Tests
{
    [TestFixture(true)]
    [TestFixture(false)]
	public class PrimitiveReaderWriterTests
	{
        public PrimitiveReaderWriterTests(bool buffered)
        {
            this.buffered = buffered;
        }

		[Test]
		public void ShouldWriteAndReadInts(
			[Values(1, 10, 100, 10000, 1000*1000)]
			int numberOfInts)
		{
			var randomInts = Helpers.GetRandomIntegers(numberOfInts);

			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < randomInts.Length; i++)
				{
					writer.Write(randomInts[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < randomInts.Length; i++)
				{
					Assert.AreEqual(randomInts[i], reader.ReadInt32());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldWriteAndReadLongs(
			[Values(1, 10, 100, 10000, 1000*1000)]
			int numberOfLongs)
		{
			var randomLongs = Helpers.GetRandomLongs(numberOfLongs);
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < randomLongs.Length; i++)
				{
					writer.Write(randomLongs[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < randomLongs.Length; i++)
				{
					var read = reader.ReadInt64();
					Assert.AreEqual(randomLongs[i], read);
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

        [Test]
        public void ShouldWriteAndReadULongs(
            [Values(1, 10, 100, 10000, 1000*1000)]
            int numberOfULongs)
        {
            var randomULongs = Helpers.GetRandomLongs(numberOfULongs).Select(x=>(ulong)x).ToArray();
            var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
            {
                for(var i = 0; i < randomULongs.Length; i++)
                {
                    writer.Write(randomULongs[i]);
                }
            }
            var position = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
            {
                for(var i = 0; i < randomULongs.Length; i++)
                {
                    var read = reader.ReadUInt64();
                    Assert.AreEqual(randomULongs[i], read);
                }
            }
            Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
        }

		[Test]
		public void ShouldWriteAndReadStrings(
			[Values(1, 100, 10000)]
			int numberOfStrings,
			[Values(true, false)]
			bool withLongStrings)
		{
			const int maxLength = 100;
			const int longStringLength = 8000;
			const int longStringProbability = 10;

			var random = Helpers.Random;
			var strings = new string[numberOfStrings];
			for(var i = 0; i < strings.Length; i++)
			{
				int length;
				if(withLongStrings && random.Next()%longStringProbability == 0)
				{
					length  = longStringLength;
				}
				else
				{
					length = random.Next(maxLength);
				}
				strings[i] = Helpers.GetRandomString(length);
			}

			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < strings.Length; i++)
				{
					writer.Write(strings[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < strings.Length; i++)
				{
					Assert.AreEqual(strings[i], reader.ReadString());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldWriteAndReadNegativeInt()
		{
			var value = -Helpers.Random.Next();
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				writer.Write(value);
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				Assert.AreEqual(value, reader.ReadInt32());
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldWriteAndReadDoubles(
			[Values(1, 10, 100, 10000, 1000*1000)]
			int numberOfDoubles)
		{
			var randomDoubles = Helpers.GetRandomDoubles(numberOfDoubles);
		
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < randomDoubles.Length; i++)
				{
					writer.Write(randomDoubles[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < randomDoubles.Length; i++)
				{
					Assert.AreEqual(randomDoubles[i], reader.ReadDouble());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

        [Test]
        public void ShouldWriteAndReadDecimal(
            [Values(1, 10, 100, 10000, 1000*1000)]
            int numberOfDecimals)
        {
            var randomDecimals = Helpers.GetRandomDecimals(numberOfDecimals);
            var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
            {
                for(var i = 0; i < randomDecimals.Length; i++)
                {
                    writer.Write(randomDecimals[i]);
                }
            }
            var position = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
            {
                for(var i = 0; i < randomDecimals.Length; i++)
                {
                    Assert.AreEqual(randomDecimals[i], reader.ReadDecimal());
                }
            }
            Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
        }

		[Test]
		public void ShouldSerializeDateTime(
			[Values(1, 10, 100, 10000)]
			int numberOfEntries)
		{
			var randomDateTimes = Helpers.GetRandomDateTimes(numberOfEntries);
		
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < randomDateTimes.Length; i++)
				{
					writer.Write(randomDateTimes[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < randomDateTimes.Length; i++)
				{
					Assert.AreEqual(randomDateTimes[i], reader.ReadDateTime());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldSerializeTimeSpan(
			[Values(1, 10, 100, 10000)]
			int numberOfEntries)
		{
			var randomDateTimes = Helpers.GetRandomDateTimes(numberOfEntries);
		
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < randomDateTimes.Length; i++)
				{
					writer.Write(randomDateTimes[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < randomDateTimes.Length; i++)
				{
					Assert.AreEqual(randomDateTimes[i], reader.ReadDateTime());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldWriteAndReadByteArray(
			[Values(10, 1000, 100000)]
			int count
			)
		{
			var stream = new MemoryStream();
			byte[] array;
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				array = new byte[count];
				Helpers.Random.NextBytes(array);
				writer.Write(array);
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			byte[] copy;
            using(var reader = new PrimitiveReader(stream, buffered))
			{
				copy = reader.ReadBytes(count);
			}
			CollectionAssert.AreEqual(array, copy);
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldWriteAndReadGuid(
			[Values(10, 1000, 100000)]
			int count
			)
		{
			var stream = new MemoryStream();
			var array = new Guid[count];
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < count; i++)
				{
					var guid = Guid.NewGuid();
					array[i] = guid;
					writer.Write(guid);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < count; i++)
				{
					Assert.AreEqual(array[i], reader.ReadGuid());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldWriteAndReadPartsOfByteArrays()
		{
			var arrays = new byte[100][];
			for(var i = 0; i < arrays.Length; i++)
			{
				arrays[i] = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
			}

			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				for(var i = 0; i < arrays.Length; i++)
				{
					writer.Write(arrays[i], i, arrays[i].Length - i);
				}
			}

			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);
            using(var reader = new PrimitiveReader(stream, buffered))
			{
				for(var i = 0; i < arrays.Length; i++)
				{
					var writtenLength = arrays[i].Length - i;
					var writtenArray = reader.ReadBytes(writtenLength);
					var subarray = new byte[writtenLength];
					Array.Copy(arrays[i], i, subarray, 0, writtenLength);
					CollectionAssert.AreEqual(subarray, writtenArray);
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldReadAndWriteLimits()
		{
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				writer.Write(byte.MinValue);
				writer.Write(byte.MaxValue);
				writer.Write(sbyte.MinValue);
				writer.Write(sbyte.MaxValue);
				writer.Write(short.MinValue);
				writer.Write(short.MaxValue);
				writer.Write(ushort.MinValue);
				writer.Write(ushort.MaxValue);
				writer.Write(int.MinValue);
				writer.Write(int.MaxValue);
				writer.Write(uint.MinValue);
				writer.Write(uint.MaxValue);
				writer.Write(long.MinValue);
				writer.Write(long.MaxValue);
				writer.Write(ulong.MinValue);
				writer.Write(ulong.MaxValue);
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);
            using(var reader = new PrimitiveReader(stream, buffered))
			{
				Assert.AreEqual(byte.MinValue, reader.ReadByte());
				Assert.AreEqual(byte.MaxValue, reader.ReadByte());
				Assert.AreEqual(sbyte.MinValue, reader.ReadSByte());
				Assert.AreEqual(sbyte.MaxValue, reader.ReadSByte());
				Assert.AreEqual(short.MinValue, reader.ReadInt16());
				Assert.AreEqual(short.MaxValue, reader.ReadInt16());
				Assert.AreEqual(ushort.MinValue, reader.ReadUInt16());
				Assert.AreEqual(ushort.MaxValue, reader.ReadUInt16());
				Assert.AreEqual(int.MinValue, reader.ReadInt32());
				Assert.AreEqual(int.MaxValue, reader.ReadInt32());
				Assert.AreEqual(uint.MinValue, reader.ReadUInt32());
				Assert.AreEqual(uint.MaxValue, reader.ReadUInt32());
				Assert.AreEqual(long.MinValue, reader.ReadInt64());
				Assert.AreEqual(long.MaxValue, reader.ReadInt64());
				Assert.AreEqual(ulong.MinValue, reader.ReadUInt64());
				Assert.AreEqual(ulong.MaxValue, reader.ReadUInt64());
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

		[Test]
		public void ShouldHandleNotAlignedWrites()
		{
			const int iterationCount = 80000;
			var stream = new MemoryStream();
            using(var writer = new PrimitiveWriter(stream, buffered))
			{
				writer.Write((byte)1);
				for(var i = 0; i < iterationCount; i++)
				{
					writer.Write(int.MaxValue);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);
            using(var reader = new PrimitiveReader(stream, buffered))
			{
				Assert.AreEqual((byte)1, reader.ReadByte());
				for(var i = 0; i < iterationCount; i++)
				{
					Assert.AreEqual(int.MaxValue, reader.ReadInt32());
				}
			}
			Assert.AreEqual(position, stream.Position, StreamCorruptedMessage);
		}

        [Test]
        public void ShouldCopyFromStream()
        {
            var stream = new MemoryStream();
            var testArray = Enumerable.Range(0, 1000).Select(x => (byte)x).ToArray();
            var testStream = new MemoryStream(testArray);
            using(var writer = new PrimitiveWriter(stream, buffered))
            {
                writer.CopyFrom(testStream, testArray.Length);
            }
            stream.Seek(0, SeekOrigin.Begin);
            var secondStream = new MemoryStream(testArray.Length);
            using(var reader = new PrimitiveReader(stream, buffered))
            {
                reader.CopyTo(secondStream, testArray.Length);
            }
            CollectionAssert.AreEqual(testArray, secondStream.ToArray());
        }

        [Test]
        public void ShouldThrowIfStreamPrematurelyFinishes()
        {
            var streamToRead = new MemoryStream();
            var streamToWrite = new MemoryStream();
            using(var reader = new PrimitiveReader(streamToRead, buffered))
            {
                Assert.Throws<EndOfStreamException>(() => reader.CopyTo(streamToWrite, 100));
            }
        }

        private readonly bool buffered;

		private const string StreamCorruptedMessage = "Stream was corrupted during read (in terms of position).";
	}
}

