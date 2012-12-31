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
using System;
using NUnit.Framework;
using System.IO;

namespace AntMicro.Migrant.PerformanceTests
{
	[TestFixture(TestType.Serialization, SerializerType.MigrantReflection)]
	[TestFixture(TestType.Deserialization, SerializerType.MigrantReflection)]
	[TestFixture(TestType.Serialization, SerializerType.MigrantGenerated)]
	[TestFixture(TestType.Serialization, SerializerType.ProtoBuf)]
	[TestFixture(TestType.Deserialization, SerializerType.ProtoBuf)]
	public class Tests : BaseFixture
	{
		public Tests(TestType testType, SerializerType serializerType)
		{
			this.testType = testType;
			this.serializerType = serializerType;
			serializer = new SerializerFactory().Produce(serializerType);
		}

		[Test]
		public void SimpleStructArray()
		{
			var structArray = new SimpleStruct[30000];
			for(var i = 0; i < structArray.Length; i++)
			{
				structArray[i].A = i;
				structArray[i].B = 1.0/i;
			}
			Test(structArray);
		}

		[Test]
		public void SimpleClassArray()
		{
			var classArray = new SimpleClass[30000];
			for(var i = 0; i < classArray.Length; i++)
			{
				var simpleClass = new SimpleClass { A = i, B = 1.0/i };
				classArray[i] = simpleClass;
			}
			Test(classArray);
		}

		private void Test<T>(T whatToTest)
		{
			var stream = new MemoryStream();
			var testSuffix = string.Format("{0}, {1}", testType, serializerType);
			if(testType == TestType.Serialization)
			{
				Run(() =>
			    {
					serializer.Serialize(whatToTest, stream);
				}, testSuffix, after: () => stream.Seek(0, SeekOrigin.Begin));
			}
			else
			{
				serializer.Serialize(whatToTest, stream);
				Run(() =>
				    {
					serializer.Deserialize<T>(stream);
				}, testSuffix, before: () => stream.Seek(0, SeekOrigin.Begin));
			}
		}

		private Customization.Settings SettingsFromFields
		{
			get
			{
				var method = serializerType == SerializerType.MigrantGenerated ? Customization.Method.Generated : Customization.Method.Reflection;
				var settings = new Customization.Settings(method, method);
				return settings;
			}
		}
		
		private readonly TestType testType;
		private readonly SerializerType serializerType;
		private readonly ISerializer serializer;
	}
	
	public enum TestType
	{
		Serialization,
		Deserialization
	}

	[ProtoBuf.ProtoContract]
	public struct SimpleStruct
	{
		[ProtoBuf.ProtoMember(1)]
		public int A;
		[ProtoBuf.ProtoMember(2)]
		public double B;
	}

	[ProtoBuf.ProtoContract]
	public class SimpleClass
	{
		[ProtoBuf.ProtoMember(1)]
		public int A;

		[ProtoBuf.ProtoMember(2)]
		public double B;
	}
}

