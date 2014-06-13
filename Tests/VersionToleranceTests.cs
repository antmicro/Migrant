/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

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
using System.Reflection.Emit;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant.Tests
{
	[Serializable]
	[TestFixture(false, false)]
	[TestFixture(true, false)]
	[TestFixture(false, true)]
	[TestFixture(true, true)]
    public class VersionToleranceTests : TwoDomainsDriver
	{
        public VersionToleranceTests(bool useGeneratedSerializer, bool useGeneratedDeserializer) : base(useGeneratedSerializer, useGeneratedDeserializer)
		{
		}

        public VersionToleranceTests() : base(true, true)
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
            [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                DynamicClass.Create("A"),
                DynamicClass.Create("A", DynamicClass.Create("BaseA")),
                vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.InheritanceChainChange) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestBaseClassRemoval(
            [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                DynamicClass.Create("A", DynamicClass.Create("BaseA")),
                DynamicClass.Create("A"),
                vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.InheritanceChainChange) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestBaseClassNameChanged(
            [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                DynamicClass.Create("A", DynamicClass.Create("BaseA")),
                DynamicClass.Create("A", DynamicClass.Create("NewBaseA")),
                vtl);

            Assert.IsTrue(
                vtl.HasFlag(VersionToleranceLevel.TypeNameChanged)
                ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestFieldMovedBetweenClasses(
            [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
        {
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(
                DynamicClass.Create("A", DynamicClass.Create("BaseA").WithField("a", typeof(string))).WithField("b", typeof(int)),
                DynamicClass.Create("A", DynamicClass.Create("BaseA")).WithField("a", typeof(string)).WithField("b", typeof(int)),
                vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.FieldMove) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestSimpleFieldAddition(
            [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
        {
            var type1 = DynamicClass.Create("A").WithField<int>("a");
            var type2 = DynamicClass.Create("A").WithField<int>("a").WithField<float>("b");

            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(type1, type2, vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.FieldAddition) ? deserializationOK : !deserializationOK);
        }

        [Test]
        public void TestSimpleFieldRemoval(
            [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
        {
            var type1 = DynamicClass.Create("A").WithField<int>("a").WithField<float>("b");
            var type2 = DynamicClass.Create("A").WithField<int>("a");
            var deserializationOK = SerializeAndDeserializeOnTwoAppDomains(type1, type2, vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.FieldRemoval) ? deserializationOK : !deserializationOK);
        }

		[Test]
		public void TestGuidVerification(
        [Values(VersionToleranceLevel.InheritanceChainChange,
                VersionToleranceLevel.ExactLayout,
                VersionToleranceLevel.FieldAddition,
                VersionToleranceLevel.FieldMove,
                VersionToleranceLevel.FieldRemoval,
                VersionToleranceLevel.Guid,
                VersionToleranceLevel.TypeNameChanged)] VersionToleranceLevel vtl)
		{
            var type = DynamicClass.Create("A").WithField<int>("Field");
            var result = SerializeAndDeserializeOnTwoAppDomains(type, type, vtl);

            Assert.IsTrue(vtl.HasFlag(VersionToleranceLevel.Guid) ? !result : result);
		}
	}
}

