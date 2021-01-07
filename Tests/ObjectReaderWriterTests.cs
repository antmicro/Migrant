//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using NUnit.Framework;
using System.IO;

namespace Antmicro.Migrant.Tests
{
	[TestFixture]
	public class ObjectReaderWriterTests
	{
		[Test]
		public void ShouldHandleTwoWritesAndReads()
		{
			var strings = new [] { "One", "Two" };

			var stream = new MemoryStream();
            var writer = new ObjectWriter(stream, Serializer.GetReflectionBasedWriteMethods());
            writer.WriteObject(strings[0]);
            writer.WriteObject(strings[1]);
            writer.Flush();
            var position = stream.Position;

			stream.Seek(0, SeekOrigin.Begin);
            var reader = new ObjectReader(stream, Serializer.GetReflectionBasedReadMethods(false));
            Assert.AreEqual(strings[0], reader.ReadObject<string>());
            Assert.AreEqual(strings[1], reader.ReadObject<string>());
            reader.Flush();

            Assert.AreEqual(position, stream.Position);
		}
	}
}

