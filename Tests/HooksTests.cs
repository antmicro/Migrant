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
using AntMicro.Migrant.Hooks;
using NUnit.Framework;
using System.IO;

namespace AntMicro.Migrant.Tests
{
	[TestFixture]
	public class HooksTests
	{
		// TODO: do this tests for generated/reflection serializer

		[Test]
		public void ShouldInvokePreSerialization()
		{
			var mock = new PreSerializationMock();
			var copy = Serializer.DeepClone(mock);
			Assert.IsTrue(mock.Invoked);
			Assert.IsTrue(copy.Invoked);
		}

		[Test]
		public void ShouldInvokePostSerialization()
		{
			var mock = new PostSerializationMock();
			var copy = Serializer.DeepClone(mock);
			Assert.IsTrue(mock.Invoked);
			Assert.IsFalse(copy.Invoked);
		}

		[Test]
		public void ShouldInvokeStaticPostSerialization()
		{
			var mock = new StaticPostSerializationMock();
			Serializer.DeepClone(mock);
			Assert.IsTrue(StaticPostSerializationMock.Invoked);
		}

		[Test]
		public void ShouldInvokePostDeserialization()
		{
			var mock = new PostDeserializationMock();
			var copy = Serializer.DeepClone(mock);
			Assert.IsFalse(mock.Invoked);
			Assert.IsTrue(copy.Invoked);
		}

		[Test]
		public void ShouldInvokeGlobalHooks()
		{
			var memoryStream = new MemoryStream();
			var serializer = new Serializer();
			var preSerializationCounter = 0;
			var postSerializationCounter = 0;
			var postDeserializationCounter = 0;
			serializer.OnPreSerialization += x => preSerializationCounter++;
			serializer.OnPostSerialization += x => postSerializationCounter++;
			serializer.OnPostDeserialization += x => postDeserializationCounter++;

			serializer.Serialize(new [] { "One", "Two" }, memoryStream);
			Assert.AreEqual(3, preSerializationCounter);
			Assert.AreEqual(3, postSerializationCounter);
			Assert.AreEqual(0, postDeserializationCounter);

			memoryStream.Seek(0, SeekOrigin.Begin);
			serializer.Deserialize<string[]>(memoryStream);
			Assert.AreEqual(3, postDeserializationCounter);
		}
	}

	public class PreSerializationMock
	{
		[PreSerialization]
		private void PreSerialization()
		{
			Invoked = true;
		}

		public bool Invoked { get; private set; }
	}

	public class PostSerializationMock
	{
		[PostSerialization]
		private void PostSerialization()
		{
			Invoked = true;
		}

		public bool Invoked { get; private set; }
	}

	public class StaticPostSerializationMock
	{
		[PostSerialization]
		private static void PostSerialization()
		{
			Invoked = true;
		}

		public static bool Invoked { get; private set; }
	}

	public class PostDeserializationMock
	{
		[PostDeserialization]
		private void PostDeserialization()
		{
			Invoked = true;
		}

		public bool Invoked { get; private set; }
	}
}

