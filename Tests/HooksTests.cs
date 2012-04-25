using AntMicro.Migrant.Hooks;
using NUnit.Framework;
using System.IO;

namespace AntMicro.Migrant.Tests
{
	[TestFixture]
	public class HooksTests
	{
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

