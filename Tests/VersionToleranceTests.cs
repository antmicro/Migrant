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
	public class VersionToleranceTests
	{
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
		}

		[Test]
		public void TestSimpleFieldAddition()
		{
			var fields = new List<Tuple<string, Type>>();
			fields.Add(Tuple.Create(Field1Name, typeof(int)));
			testsOnDomain1.BuildTypeOnAppDomain(TypeName, fields);
			var fieldCheck = new FieldCheck(Field1Name, 666);
			testsOnDomain1.SetValueOnAppDomain(fieldCheck);
			var data = testsOnDomain1.SerializeOnAppDomain();

			fields.Add(Tuple.Create(Field2Name, typeof(int)));
			testsOnDomain2.BuildTypeOnAppDomain(TypeName, fields);

			testsOnDomain2.DeserializeOnAppDomain(data, new [] { fieldCheck, new FieldCheck(Field2Name, 0) });
		}

		[Test]
		public void TestSimpleFieldRemoval()
		{
			var fields = new List<Tuple<string, Type>>();
			fields.Add(Tuple.Create(Field1Name, typeof(int)));
			testsOnDomain2.BuildTypeOnAppDomain(TypeName, fields);

			fields.Add(Tuple.Create(Field2Name, typeof(int)));
			testsOnDomain1.BuildTypeOnAppDomain(TypeName, fields);
			var field1Check = new FieldCheck(Field1Name, 667);
			testsOnDomain1.SetValueOnAppDomain(field1Check);
			testsOnDomain1.SetValueOnAppDomain(new FieldCheck(Field2Name, 668));
			var data = testsOnDomain1.SerializeOnAppDomain();

			testsOnDomain2.DeserializeOnAppDomain(data, new [] { field1Check });
		}

		public void BuildTypeOnAppDomain(string typeName, IEnumerable<Tuple<string, Type>> fields)
		{
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(AssemblyName, AssemblyBuilderAccess.RunAndSave);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule(AssemblyName.Name + ".dll");
			var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public);
			foreach(var field in fields)
			{
				typeBuilder.DefineField(field.Item1, field.Item2, FieldAttributes.Public);
			}
			builtType = typeBuilder.CreateType();
			obj = Activator.CreateInstance(builtType);
			assemblyBuilder.Save(AssemblyName.Name + ".dll"); // TODO: file.Delete?
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

		public void DeserializeOnAppDomain(byte[] data, IEnumerable<FieldCheck> fieldsToCheck)
		{
			var stream = new MemoryStream(data);
			var settings = new Settings(ignoreModuleIdInequality: true); // TODO
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

		private const string Field1Name = "Field1";
		private const string Field2Name = "Field2";
		private static readonly AssemblyName AssemblyName = new AssemblyName("TestAssembly");
	}
}

