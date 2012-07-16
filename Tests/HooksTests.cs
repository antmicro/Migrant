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
	[TestFixture(false, false)]
	[TestFixture(true, false)]
	public class HooksTests
	{
		public HooksTests(bool useGeneratedSerializer, bool useGeneratedDeserializer)
		{
			this.useGeneratedDeserializer = useGeneratedDeserializer;
			this.useGeneratedSerializer = useGeneratedSerializer;
		}

		[Test]
		public void ShouldInvokePreSerialization()
		{
			var mock = new PreSerializationMock();
			var copy = SerializerClone(mock);
			Assert.IsTrue(mock.Invoked);
			Assert.IsTrue(copy.Invoked);
		}

		[Test]
		public void ShouldInvokeDerivedPreSerialization()
		{
			var mock = new PreSerializationMockDerived();
			var copy = SerializerClone(mock);
			Assert.IsTrue(mock.Invoked);
			Assert.IsTrue(mock.DerivedInvoked);
			Assert.IsTrue(copy.Invoked);
			Assert.IsTrue(copy.DerivedInvoked);
		}

		[Test]
		public void ShouldInvokePostSerialization()
		{
			var mock = new PostSerializationMock();
			var copy = SerializerClone(mock);
			Assert.IsTrue(mock.Invoked);
			Assert.IsFalse(copy.Invoked);
		}

		[Test]
		public void ShouldInvokeDerivedPostSerialization()
		{
			var mock = new PostSerializationMockDerived();
			var copy = SerializerClone(mock);
			Assert.IsTrue(mock.Invoked);
			Assert.IsTrue(mock.DerivedInvoked);
			Assert.IsFalse(copy.Invoked);
			Assert.IsFalse(copy.DerivedInvoked);
		}

		[Test]
		public void ShouldInvokeStaticPostSerialization()
		{
			var mock = new StaticPostSerializationMock();
			SerializerClone(mock);
			Assert.IsTrue(StaticPostSerializationMock.Invoked);
		}

		[Test]
		public void ShouldInvokePostDeserialization()
		{
			var mock = new PostDeserializationMock();
			var copy = SerializerClone(mock);
			Assert.IsFalse(mock.Invoked);
			Assert.IsTrue(copy.Invoked);
		}

		[Test]
		public void ShouldInvokeDerivedPostDeserialization()
		{
			var mock = new PostDeserializationMockDerived();
			var copy = SerializerClone(mock);
			Assert.IsFalse(mock.Invoked);
			Assert.IsFalse(mock.DerivedInvoked);
			Assert.IsTrue(copy.Invoked);
			Assert.IsTrue(copy.DerivedInvoked);
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

		private T SerializerClone<T>(T toClone)
		{
			var settings = SettingsFromFields;
			return Serializer.DeepClone(toClone, settings);
		}
		
		private Customization.Settings SettingsFromFields
		{
			get
			{
				var settings = new Customization.Settings
					(
						useGeneratedSerializer ? Customization.Method.Generated : Customization.Method.Reflection,
						useGeneratedDeserializer ? Customization.Method.Generated : Customization.Method.Reflection
						);
				return settings;
			}
		}
		
		private bool useGeneratedSerializer;
		private bool useGeneratedDeserializer;
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

	public class PreSerializationMockDerived : PreSerializationMock
	{
		[PreSerialization]
		private void PreSerializationDerived()
		{
			DerivedInvoked = true;
		}

		public bool DerivedInvoked { get; private set; }
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

	public class PostSerializationMockDerived : PostSerializationMock
	{
		[PostSerialization]
		private void PostSerializationDerived()
		{
			DerivedInvoked = true;
		}

		public bool DerivedInvoked { get; private set; }
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

	public class PostDeserializationMockDerived : PostDeserializationMock
	{
		[PostDeserialization]
		private void PostDeserializationDerived()
		{
			DerivedInvoked = true;
		}

		public bool DerivedInvoked { get; private set; }
	}
}

