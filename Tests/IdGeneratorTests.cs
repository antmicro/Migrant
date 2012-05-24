using System;
using NUnit.Framework;
using System.Collections.Generic;

namespace AntMicro.AntSerializer.Tests
{
	[TestFixture]
	public class IdGeneratorTests
	{
		[Test]
		public void ShouldGenerateUniqueId()
		{
			const int ObjectsNo = 100000;
			var identifier = new ObjectIdentifier();
			var objects = new object[ObjectsNo];
			for(var i = 0; i < objects.Length; i++)
			{
				objects[i] = new object();
			}
			var generated = new HashSet<int>();
			foreach(var obj in objects)
			{
				var id = identifier.GetId(obj);
				Assert.IsTrue(generated.Add(id), "Not unique ID was generated.");
			}
		}
	}
}

