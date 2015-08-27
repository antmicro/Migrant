/*
  Copyright (c) 2013-2015 Antmicro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Mateusz Holenko (mholenko@gmail.com)

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
using Antmicro.Migrant.Customization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Antmicro.Migrant.Tests
{
    [Serializable]
    [TestFixture(false, false, true)]
    [TestFixture(true, false, true)]
    [TestFixture(false, true, true)]
    [TestFixture(true, true, true)]
    [TestFixture(false, false, false)]
    [TestFixture(true, false, false)]
    [TestFixture(false, true, false)]
    [TestFixture(true, true, false)]
    [Category("MultiAssemblyTests")]
    public class VersionToleranceTests : TwoDomainsDriver
    {
        public VersionToleranceTests(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useStamping) : base(useGeneratedSerializer, useGeneratedDeserializer, useStamping)
        {
        }

        public VersionToleranceTests() : base(true, true, true)
        {
        }

        [SetUp]
        public void SetUp()
        {
            PrepareDomains();
        }

        [TearDown]
        public void TearDown()
        {
            DisposeDomains();
        }

        [Test]
        public void TestBaseClassInsertion(
            [Values(
                0,
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange
            )] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                                        DynamicType.CreateClass("A"),
                                        DynamicType.CreateClass("A", DynamicType.CreateClass("BaseA")),
                                        vtl);

            Assert.AreEqual(vtl.HasFlag(VersionToleranceLevel.AllowInheritanceChainChange), deserializationOK);
        }

        [Test]
        public void TestBaseClassRemoval(
            [Values(
                0,
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange
            )] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                                        DynamicType.CreateClass("A", DynamicType.CreateClass("BaseA")),
                                        DynamicType.CreateClass("A", additionalTypes: new [] { DynamicType.CreateClass("BaseA") }),
                                        vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.AllowInheritanceChainChange) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestBaseClassNameChanged(
            [Values(
                0,
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange
            )] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                                        DynamicType.CreateClass("A", DynamicType.CreateClass("BaseA")),
                                        DynamicType.CreateClass("A", DynamicType.CreateClass("NewBaseA"), additionalTypes: new [] { DynamicType.CreateClass("BaseA") }),
                                        vtl);

            Assert.IsTrue(
                vtl.HasFlag(VersionToleranceLevel.AllowInheritanceChainChange)
                ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestFieldMovedBetweenClasses(
            [Values(
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange,
                VersionToleranceLevel.AllowFieldAddition | VersionToleranceLevel.AllowFieldRemoval
            )] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                                        DynamicType.CreateClass("A", DynamicType.CreateClass("BaseA").WithField("a", typeof(string))).WithField("b", typeof(int)),
                                        DynamicType.CreateClass("A", DynamicType.CreateClass("BaseA")).WithField("a", typeof(string)).WithField("b", typeof(int)),
                                        vtl);

            Assert.AreEqual(
                vtl.HasFlag(VersionToleranceLevel.AllowFieldAddition) && vtl.HasFlag(VersionToleranceLevel.AllowFieldRemoval),
                deserializationOK);
        }

        [Test]
        public void TestSimpleFieldAddition(
            [Values(
                0,
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange
            )] VersionToleranceLevel vtl)
        {
            var type1 = DynamicType.CreateClass("A").WithField<int>("a");
            var type2 = DynamicType.CreateClass("A").WithField<int>("a").WithField<float>("b");

            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(type1, type2, vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.AllowFieldAddition) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestSimpleFieldRemoval(
            [Values(
                0,
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange
            )] VersionToleranceLevel vtl)
        {
            var type1 = DynamicType.CreateClass("A").WithField<int>("a").WithField<float>("b");
            var type2 = DynamicType.CreateClass("A").WithField<int>("a");
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(type1, type2, vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.AllowFieldRemoval) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestGuidVerification(
            [Values(
                0,
                VersionToleranceLevel.AllowInheritanceChainChange,
                VersionToleranceLevel.AllowFieldAddition,
                VersionToleranceLevel.AllowFieldRemoval,
                VersionToleranceLevel.AllowGuidChange
            )] VersionToleranceLevel vtl)
        {
            var type = DynamicType.CreateClass("A").WithField<int>("Field");
            var result = SerializeAndDeserializeOnTwoAppDomains(type, type, vtl, false);

            Assert.AreEqual(vtl.HasFlag(VersionToleranceLevel.AllowGuidChange), result);
        }

        [Test]
        public void ShouldNotStampTypeTwice()
        {
            var serializer = new Serializer(GetSettings());
            var memoryStream = new MemoryStream();
            var o = new ClassWithGenerics();
            serializer.Serialize(o, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            var strings = GetString(memoryStream.GetBuffer());

            Assert.AreEqual(1, NumberOfOccurences(strings, "System.Int32"));
        }

        [Test]
        public void ShouldNotStampGenericTypeTwice()
        {
            var serializer = new Serializer(GetSettings());
            var memoryStream = new MemoryStream();
            var o = new ClassWithGenerics();
            serializer.Serialize(o, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            var strings = GetString(memoryStream.GetBuffer());

            Assert.AreEqual(1, NumberOfOccurences(strings, "System.Collections.Generic.List`1"));
        }

        private static string GetString(byte[] array)
        {
            // 32 - ' ', 126 - '~'
            var interestingBytes = array.Where(b => b >= 32 && b <= 126).ToArray();
            return Encoding.ASCII.GetString(interestingBytes);
        }

        private static int NumberOfOccurences(string src, string pattern)
        {
            var n = 0;
            var count = 0;
            while((n = src.IndexOf(pattern, n)) != -1)
            {
               n++;
               count++;
            }
            return count;
        }

        private class ClassWithGenerics
        {
            #pragma warning disable 649
            public int IntegerNumber;
            public int SecondIntegerNumber;
            public List<int> ListOfIntegerNumbers;
            public float FloatNumber;
            public List<float> ListOfFloatNumbers;
            #pragma warning restore 649
        }
    }
}

