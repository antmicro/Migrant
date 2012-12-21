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
	[TestFixture(TestType.Serialization | TestType.Reflection)]
	[TestFixture(TestType.Serialization | TestType.Generated)]
	[TestFixture(TestType.Deserialization | TestType.Reflection)]
	public class Tests : BaseFixture
	{
		public Tests(TestType testType)
		{
			this.testType = testType;
		}		

		[Test]
		public void SimpleStructSet()
		{
			var structArray = new SimpleStruct[10000];
			for(var i = 0; i < structArray.Length; i++)
			{
				structArray[i].A = i;
				structArray[i].B = 1.0/i;
			}
			TestClone(structArray);
		}

		private void TestClone<T>(T toClone)
		{
			var serializer = new Serializer(SettingsFromFields);
			var stream = new MemoryStream();
			var testSuffix = testType.ToString();
			if((testType & TestType.Deserialization) == 0)
			{
				Run(() =>
				{
					serializer.Serialize(toClone, stream);
				}, testSuffix, after: () => stream.Seek(0, SeekOrigin.Begin));
			}
			else
			{
				serializer.Serialize(toClone, stream);
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
				var isGenerated = (testType & TestType.Generated) != 0;
				var settings = new Customization.Settings
					(
						isGenerated ? Customization.Method.Generated : Customization.Method.Reflection,
						isGenerated ? Customization.Method.Generated : Customization.Method.Reflection
						);
				return settings;
			}
		}
		
		private readonly TestType testType;
	}

	[Flags]
	public enum TestType
	{
		Reflection = 1,
		Generated = 2,
		Serialization = 4,
		Deserialization = 8
	}
	
	public struct SimpleStruct
	{
		public int A;
		public double B;
	}
}

