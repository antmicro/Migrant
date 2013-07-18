using System;
using System.Reflection.Emit;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using AntMicro.Migrant.Customization;

namespace AntMicro.Migrant.Tests
{
	[Serializable]
	[TestFixture(false, false)]
	[TestFixture(true, false)]
	[TestFixture(false, true)]
	[TestFixture(true, true)]
	public class VersionToleranceTests : MarshalByRefObject
	{
		public VersionToleranceTests(bool useGeneratedSerializer, bool useGeneratedDeserializer)
		{
			this.useGeneratedDeserializer = useGeneratedDeserializer;
			this.useGeneratedSerializer = useGeneratedSerializer;
		}

		public VersionToleranceTests()
		{

		}

		[SetUp]
		public void SetUp()
		{
			domain1 = AppDomain.CreateDomain("domain1");
			domain2 = AppDomain.CreateDomain("domain2");
			testsOnDomain1 = (VersionToleranceTests)domain1.CreateInstanceAndUnwrap(typeof(VersionToleranceTests).Assembly.FullName, typeof(VersionToleranceTests).FullName);
			testsOnDomain2 = (VersionToleranceTests)domain2.CreateInstanceAndUnwrap(typeof(VersionToleranceTests).Assembly.FullName, typeof(VersionToleranceTests).FullName);
		}

		[TearDown]
		public void TearDown()
		{
			testsOnDomain1 = null;
			testsOnDomain2 = null;
			AppDomain.Unload(domain1);
			AppDomain.Unload(domain2);
			File.Delete(AssemblyName.Name + ".dll");
		}

		[Test]
		public void TestSimpleFieldAddition(
			[Values(VersionToleranceLevel.Exact, VersionToleranceLevel.FieldAddition, VersionToleranceLevel.FieldAdditionAndRemoval)] VersionToleranceLevel versionToleranceLevel)
		{
			var fields = new List<Tuple<string, Type>>();
			fields.Add(Tuple.Create(Field1Name, typeof(int)));
			testsOnDomain1.BuildTypeOnAppDomain(TypeName, fields, false);
			var fieldCheck = new FieldCheck(Field1Name, 666);
			testsOnDomain1.SetValueOnAppDomain(fieldCheck);
			var data = testsOnDomain1.SerializeOnAppDomain();

			fields.Add(Tuple.Create(Field2Name, typeof(int)));
			testsOnDomain2.BuildTypeOnAppDomain(TypeName, fields, true);

			TestDelegate deserialization = () => testsOnDomain2.DeserializeOnAppDomain(data, new [] { fieldCheck, new FieldCheck(Field2Name, 0) }, GetSettings(versionToleranceLevel));
			if(versionToleranceLevel == VersionToleranceLevel.Exact)
			{
				Assert.Throws<InvalidOperationException>(deserialization);
			}
			else
			{
				deserialization();
			}

		}

		[Test]
		public void TestSimpleFieldRemoval(
			[Values(VersionToleranceLevel.Exact, VersionToleranceLevel.FieldRemoval, VersionToleranceLevel.FieldAdditionAndRemoval)] VersionToleranceLevel versionToleranceLevel)
		{
			var fields = new List<Tuple<string, Type>>();
			fields.Add(Tuple.Create(Field1Name, typeof(int)));
			fields.Add(Tuple.Create(Field2Name, typeof(int)));
			testsOnDomain1.BuildTypeOnAppDomain(TypeName, fields, false);
			var field1Check = new FieldCheck(Field1Name, 667);
			testsOnDomain1.SetValueOnAppDomain(field1Check);
			testsOnDomain1.SetValueOnAppDomain(new FieldCheck(Field2Name, 668));
			var data = testsOnDomain1.SerializeOnAppDomain();

			testsOnDomain2.BuildTypeOnAppDomain(TypeName, fields.Take(1).ToArray(), true);

			TestDelegate deserialization = () => testsOnDomain2.DeserializeOnAppDomain(data, new [] { field1Check }, GetSettings(versionToleranceLevel));
			if(versionToleranceLevel == VersionToleranceLevel.Exact)
			{
				Assert.Throws<InvalidOperationException>(deserialization);
			}
			else
			{
				deserialization();
			}
		}

		public void BuildTypeOnAppDomain(string typeName, IEnumerable<Tuple<string, Type>> fields, bool persistent)
		{
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(AssemblyName, persistent ? AssemblyBuilderAccess.RunAndSave : AssemblyBuilderAccess.Run);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule(AssemblyName.Name + ".dll");
			var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public);
			foreach(var field in fields)
			{
				typeBuilder.DefineField(field.Item1, field.Item2, FieldAttributes.Public);
			}
			builtType = typeBuilder.CreateType();
			obj = Activator.CreateInstance(builtType);
			if(persistent)
			{
				assemblyBuilder.Save(AssemblyName.Name + ".dll");
			}
		}

		public void SetValueOnAppDomain(FieldCheck fieldCheck)
		{
			var fields = builtType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			fields.First(x => x.Name == fieldCheck.Name).SetValue(obj, fieldCheck.Value);
		}

		public byte[] SerializeOnAppDomain()
		{
			var stream = new MemoryStream();
			var serializer = new Serializer();
			serializer.Serialize(obj, stream);
			return stream.ToArray();
		}

		public void DeserializeOnAppDomain(byte[] data, IEnumerable<FieldCheck> fieldsToCheck, Settings settings)
		{
			var stream = new MemoryStream(data);
			var deserializer = new Serializer(settings);
			var deserializedObject = deserializer.Deserialize<object>(stream);
			var fields = deserializedObject.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			foreach(var fieldToCheck in fieldsToCheck)
			{
				var field = fields.First(x => x.Name == fieldToCheck.Name);
				var value = field.GetValue(deserializedObject);
				Assert.AreEqual(fieldToCheck.Value, value);
			}
		}

		private Settings GetSettings(VersionToleranceLevel level = 0)
		{
			return new Settings(useGeneratedSerializer ? Method.Generated : Method.Reflection,					
			                    useGeneratedDeserializer ? Method.Generated : Method.Reflection,					
			                    level);
		}

		[Serializable]
		public class FieldCheck
		{
			public FieldCheck(string name, object value)
			{
				Name = name;
				Value = value;
			}			

			public string Name { get; private set; }
			public object Value { get; private set; }
		}

		private const string TypeName = "SampleType";
		private VersionToleranceTests testsOnDomain1;
		private VersionToleranceTests testsOnDomain2;
		private AppDomain domain1;
		private AppDomain domain2;
		private Type builtType;
		private object obj;
		private bool useGeneratedSerializer;
		private bool useGeneratedDeserializer;

		private const string Field1Name = "Field1";
		private const string Field2Name = "Field2";
		private static readonly AssemblyName AssemblyName = new AssemblyName("TestAssembly");
	}
}

