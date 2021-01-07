//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using NUnit.Framework;

namespace Antmicro.Migrant.Tests
{
    [TestFixture]
    [Category("MultiAssemblyTests")]
    [TestFixture(false, false)]
    [TestFixture(true, false)]
    [TestFixture(true, true)]
    [TestFixture(false, true)]
    public class TwoDomainTests : TwoDomainsDriver
    {
        public TwoDomainTests(bool useGeneratedSerialized, bool useGeneratedDeserialzer) : base(useGeneratedSerialized, useGeneratedDeserialzer)
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
            var type1 = DynamicType.CreateClass("A").WithField<string>("a").WithField<int>("b").WithField<string>("c");
            var type2 = DynamicType.CreateClass("A").WithField<string>("a").WithField<string>("c");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("b", 147);
            testsOnDomain1.SetValueOnAppDomain("c", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowFieldRemoval));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("c"));
        }

        [Test]
        public void ShouldHandleFieldRemovalInStruct()
        {
            var type1 = DynamicType.CreateStruct("A").WithField<string>("a").WithField<int>("b").WithField<string>("c");
            var type2 = DynamicType.CreateStruct("A").WithField<string>("a").WithField<string>("c");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("b", 147);
            testsOnDomain1.SetValueOnAppDomain("c", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowFieldRemoval));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("c"));
        }

        [Test]
        public void ShouldHandleFieldRemovalInStructNestedInClass()
        {
            var type1 = DynamicType.CreateClass("A").WithField<string>("a").WithField("b", DynamicType.CreateStruct("B").WithField<string>("a").WithField<int>("b"));
            var type2 = DynamicType.CreateClass("A").WithField<string>("a").WithField("b", DynamicType.CreateStruct("B").WithField<int>("b"));

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("b.a", "text");
            testsOnDomain1.SetValueOnAppDomain("b.b", 147);

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowFieldRemoval));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual(147, testsOnDomain2.GetValueOnAppDomain("b.b"));
        }

        [Test]
        public void ShouldHandleFieldInsertion()
        {
            var type1 = DynamicType.CreateClass("A").WithField<string>("a").WithField<string>("c");
            var type2 = DynamicType.CreateClass("A").WithField<string>("a").WithField<int>("b").WithField<string>("c");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("a", "testing");
            testsOnDomain1.SetValueOnAppDomain("c", "finish");

            var bytes = testsOnDomain1.SerializeOnAppDomain();

            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowFieldAddition));

            Assert.AreEqual("testing", testsOnDomain2.GetValueOnAppDomain("a"));
            Assert.AreEqual(0, testsOnDomain2.GetValueOnAppDomain("b"));
            Assert.AreEqual("finish", testsOnDomain2.GetValueOnAppDomain("c"));
        }

        [Test]
        public void ShouldDeserializeConstructorFields()
        {
            var type1 = DynamicType.CreateClass("A").WithConstructorField<object>("f");
            var type2 = DynamicType.CreateClass("A").WithConstructorField<object>("f");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("f", new Object());

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange());

            Assert.IsNotNull(testsOnDomain2.GetValueOnAppDomain("f"));
        }

        [Test]
        public void ShouldHandleBaseClassAddition()
        {
            var type1 = DynamicType.CreateClass("A").WithField<object>("f");
            var type2 = DynamicType.CreateClass("A", DynamicType.CreateClass("X")).WithField<object>("f");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("f", new Object());

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowInheritanceChainChange));

            Assert.IsNotNull(testsOnDomain2.GetValueOnAppDomain("f"));
        }

        [Test]
        public void ShouldHandleBaseClassRemoval()
        {
            var typeX = DynamicType.CreateClass("X");

            var type1 = DynamicType.CreateClass("A", typeX).WithField<object>("f");
            var type2 = DynamicType.CreateClass("A", additionalTypes: new [] { typeX }).WithField<object>("f");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("f", new Object());

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowInheritanceChainChange));

            Assert.IsNotNull(testsOnDomain2.GetValueOnAppDomain("f"));
        }

        [Test]
        public void ShouldSerializeGenericTypeWithInterface()
        {
            var type1 = DynamicType.CreateClass("A", genericArgument: DynamicType.CreateInterface("IX")).WithField<int>("f");
            var type2 = DynamicType.CreateClass("A", genericArgument: DynamicType.CreateInterface("IX")).WithField<int>("f");

            testsOnDomain1.CreateInstanceOnAppDomain(type1);
            testsOnDomain1.SetValueOnAppDomain("f", 1);

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            testsOnDomain2.CreateInstanceOnAppDomain(type2);
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowInheritanceChainChange));

            Assert.AreEqual(1, testsOnDomain2.GetValueOnAppDomain("f"));
        }

        [Test]
        public void ShouldHandleAssemblyVersionChange()
        {
            var type1 = DynamicType.CreateClass("A").WithField("a", DynamicType.CreateClass("C").WithField<int>("c"));
            var type2 = DynamicType.CreateClass("A").WithField("a", DynamicType.CreateClass("C").WithField<int>("c"));

            testsOnDomain1.CreateInstanceOnAppDomain(type1, new Version(1, 0));
            testsOnDomain1.SetValueOnAppDomain("a.c", 1);

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            testsOnDomain2.CreateInstanceOnAppDomain(type2, new Version(1, 1));
            testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettingsAllowingGuidChange(Antmicro.Migrant.Customization.VersionToleranceLevel.AllowAssemblyVersionChange));

            Assert.AreEqual(1, testsOnDomain2.GetValueOnAppDomain("a.c"));
        }
            
    }
}

