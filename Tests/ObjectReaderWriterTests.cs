using System;
using System.Linq;
using NUnit.Framework;
using System.IO;
using System.Collections.Generic;

namespace AntMicro.Migrant.Tests
{
	[TestFixture]
	public class ObjectReaderWriterTests
	{
		[Test]
		public void ShouldHandleTwoWritesAndReads()
		{
			var strings = new [] { "One", "Two" };

			var typeIndices = new Dictionary<Type, int>
			{
				{typeof(string), 0}
			};
			var stream = new MemoryStream();
			var writer = new ObjectWriter(stream, typeIndices, true);
			writer.WriteObject(strings[0]);
			writer.WriteObject(strings[1]);
			var position = stream.Position;

			stream.Seek(0, SeekOrigin.Begin);
			var types = typeIndices.OrderBy(x => x.Value).Select(x => x.Key).ToArray();
			var reader = new ObjectReader(stream, types);
			Assert.AreEqual(strings[0], reader.ReadObject<string>());
			Assert.AreEqual(strings[1], reader.ReadObject<string>());
			Assert.AreEqual(position, stream.Position);
		}
	}
}

