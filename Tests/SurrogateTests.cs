using System;
using NUnit.Framework;
using System.IO;
using AntMicro.Migrant;
using System.Collections.Generic;

namespace AntMicro.Migrant.Tests
{
	[TestFixture(false, false)]
	[TestFixture(true, false)]
	public class SurrogateTests
	{
		public SurrogateTests(bool useGeneratedSerializer, bool useGeneratedDeserializer)
		{
			this.useGeneratedDeserializer = useGeneratedDeserializer;
			this.useGeneratedSerializer = useGeneratedSerializer;
		}

		[Test]
		public void ShouldPlaceObjectForSurrogate()
		{
			var b = new SurrogateMockB();
			var pseudocopy = PseudoClone(b, serializer =>
			                             {
				serializer.SetObjectForSurrogate<SurrogateMockB>(x => new SurrogateMockA(999));
			});
			var a = pseudocopy as SurrogateMockA;
			Assert.IsNotNull(a);
			Assert.AreEqual(999, a.Field);
		}

		[Test]
		public void ShouldPlaceObjectForSurrogatePreservingIdentity()
		{
			var b = new SurrogateMockB();
			var list = new List<object> { b, new List<object> { b }, new SurrogateMockB() };
			var counter = 0;
			var pseudocopy = PseudoClone(list, serializer =>
			                             {
				serializer.SetObjectForSurrogate<SurrogateMockB>(x => new SurrogateMockA(counter++));
			});
			list = pseudocopy as List<object>;
			Assert.IsNotNull(list);
			var sublist = list[1] as List<object>;
			Assert.IsNotNull(sublist);
			Assert.AreSame(list[0], sublist[0]);
			Assert.AreNotSame(list[0], list[2]);
			var a = list[0] as SurrogateMockA;
			Assert.IsNotNull(a);
			Assert.AreEqual(counter - 2, a.Field);
			var secondA = list[2] as SurrogateMockA;
			Assert.IsNotNull(secondA);
			Assert.AreEqual(counter - 1, secondA.Field);
		}

		// pseudo, because cloned object can be/can contain different type due to surrogate operations
		private object PseudoClone(object obj, Action<Serializer> actionsBeforeSerilization)
		{
			var serializer = new Serializer(SettingsFromFields);
			actionsBeforeSerilization(serializer);
			var mStream = new MemoryStream();
			serializer.Serialize(obj, mStream);
			mStream.Seek(0, SeekOrigin.Begin);
			return serializer.Deserialize<object>(mStream);
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

	public class SurrogateMockA
	{
		public SurrogateMockA(int field)
		{
			Field = field;
		}

		public int Field { get; private set; }
	}

	public class SurrogateMockB
	{

	}
}

