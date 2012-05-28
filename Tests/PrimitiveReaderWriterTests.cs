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

namespace AntMicro.Migrant.Tests
{
	[TestFixture]
	public class PrimitiveReaderWriterTests
	{
		[Test]
		public void ShouldWriteAndReadInts(
			[Values(1, 10, 100, 10000, 1000*1000)]
			int numberOfInts)
		{
			var randomInts = Helpers.GetRandomIntegers(numberOfInts);

			var stream = new MemoryStream();
			using(var writer = new PrimitiveWriter(stream))
			{
				for(var i = 0; i < randomInts.Length; i++)
				{
					writer.Write(randomInts[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
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
			using(var writer = new PrimitiveWriter(stream))
			{
				for(var i = 0; i < randomLongs.Length; i++)
				{
					writer.Write(randomLongs[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
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
			using(var writer = new PrimitiveWriter(stream))
			{
				for(var i = 0; i < strings.Length; i++)
				{
					writer.Write(strings[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
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
			using(var writer = new PrimitiveWriter(stream))
			{
				writer.Write(value);
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
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
			using(var writer = new PrimitiveWriter(stream))
			{
				for(var i = 0; i < randomDoubles.Length; i++)
				{
					writer.Write(randomDoubles[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
			{
				for(var i = 0; i < randomDoubles.Length; i++)
				{
					Assert.AreEqual(randomDoubles[i], reader.ReadDouble());
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
			using(var writer = new PrimitiveWriter(stream))
			{
				for(var i = 0; i < randomDateTimes.Length; i++)
				{
					writer.Write(randomDateTimes[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
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
			using(var writer = new PrimitiveWriter(stream))
			{
				for(var i = 0; i < randomDateTimes.Length; i++)
				{
					writer.Write(randomDateTimes[i]);
				}
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			using(var reader = new PrimitiveReader(stream))
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
			using(var writer = new PrimitiveWriter(stream))
			{
				array = new byte[count];
				Helpers.Random.NextBytes(array);
				writer.Write(array);
			}
			var position = stream.Position;
			stream.Seek(0, SeekOrigin.Begin);

			byte[] copy;
			using(var reader = new PrimitiveReader(stream))
			{
				copy = reader.ReadBytes(count);
			}
			CollectionAssert.AreEqual(array, copy);
			Assert.AreEqual(position, stream.Position);
		}

		private const string StreamCorruptedMessage = "Stream was corrupted during read (in terms of position).";
	}
}

