// *******************************************************************
//
//  Copyright (c) 2011-2014, Antmicro Ltd <antmicro.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// *******************************************************************
using System;
using NUnit.Framework;

namespace Antmicro.Migrant.Tests
{
    [TestFixture]
    [TestFixture(false, false)]
    [TestFixture(true, false)]
    [TestFixture(false, true)]
    [TestFixture(true, true)]
    public class TwoDomainTests : TwoDomainsDriver
    {
        public TwoDomainTests(bool useGeneratedSerialized, bool useGeneratedDeserialzer) : base(useGeneratedSerialized, useGeneratedDeserialzer)
        {
        }

        public TwoDomainTests()
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
        public void ShouldHandleFieldRemoval()
        {
            var type1 = DynamicClass.Create("A").WithField<string>("a").WithField<int>("b").WithField<string>("c");
            var type2 = DynamicClass.Create("A").WithField<string>("a").WithField<string>("c");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("b", 147);
            testsOnDomain1.SetValueOnAppDomain("c", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings(Antmicro.Migrant.Customization.VersionToleranceLevel.FieldRemoval));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("c"));
        }

        [Test]
        public void ShouldHandleFieldInsertion()
        {
            var type1 = DynamicClass.Create("A").WithField<string>("a").WithField<string>("c");
            var type2 = DynamicClass.Create("A").WithField<string>("a").WithField<int>("b").WithField<string>("c");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("c", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings(Antmicro.Migrant.Customization.VersionToleranceLevel.FieldAddition));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual(0, testsOnDomain2.GetValueOnAppDomain("b"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("c"));
        }

        [Test]
        public void ShouldHandleFieldMoveDown()
        {
            var type1 = DynamicClass.Create("A", DynamicClass.Create("Base")).WithField<string>("a").WithField<string>("b");
            var type2 = DynamicClass.Create("A", DynamicClass.Create("Base").WithField<string>("a")).WithField<string>("b");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("b", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings(Antmicro.Migrant.Customization.VersionToleranceLevel.FieldMove));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("b"));
        }

        [Test]
        public void ShouldHandleFieldMoveUp()
        {
            var type1 = DynamicClass.Create("A", DynamicClass.Create("Base").WithField<string>("a")).WithField<string>("b");
            var type2 = DynamicClass.Create("A", DynamicClass.Create("Base")).WithField<string>("a").WithField<string>("b");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("b", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings(Antmicro.Migrant.Customization.VersionToleranceLevel.FieldMove));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("b"));
        }

        [Test]
        public void ShouldHandleNewFieldMoveDown()
        {
            var type1 = DynamicClass.Create("C", DynamicClass.Create("B", DynamicClass.Create("A")).WithField<int>("f")).WithField<int>("f");
            var type2 = DynamicClass.Create("C", DynamicClass.Create("B", DynamicClass.Create("A").WithField<int>("f")).WithField<int>("f"));
                        
            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("B", "f", 200);
            testsOnDomain1.SetValueOnAppDomain("C", "f", 300);

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings(Antmicro.Migrant.Customization.VersionToleranceLevel.FieldMove));

            Assert.AreEqual(300, testsOnDomain2.GetValueOnAppDomain("A", "f"));
            Assert.AreEqual(200, testsOnDomain2.GetValueOnAppDomain("B", "f"));
        }

        [Test]
        public void ShouldHandleNewFieldMoveUp()
        {
            var type1 = DynamicClass.Create("C", DynamicClass.Create("B", DynamicClass.Create("A").WithField<int>("f")).WithField<int>("f"));
            var type2 = DynamicClass.Create("C", DynamicClass.Create("B", DynamicClass.Create("A")).WithField<int>("f")).WithField<int>("f");
                        
            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("A", "f", 100);
            testsOnDomain1.SetValueOnAppDomain("B", "f", 200);

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings(Antmicro.Migrant.Customization.VersionToleranceLevel.FieldMove));

            Assert.AreEqual(100, testsOnDomain2.GetValueOnAppDomain("C", "f"));
            Assert.AreEqual(200, testsOnDomain2.GetValueOnAppDomain("B", "f"));
        }

        [Test]
        public void ShouldDeserializeConstructorFields()
        {
            var type1 = DynamicClass.Create("A").WithConstructorField<object>("f");
            var type2 = DynamicClass.Create("A").WithConstructorField<object>("f");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("f", new Object());

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, GetSettings());

            Assert.IsNotNull(testsOnDomain2.GetValueOnAppDomain("f"));
        }
    }
}

