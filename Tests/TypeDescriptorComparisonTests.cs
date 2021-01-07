//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using NUnit.Framework;
using System.Linq;
using Antmicro.Migrant.Customization;
using Antmicro.Migrant.VersionTolerance;

namespace Antmicro.Migrant.Tests
{
    [TestFixture]
    public class TypeDescriptorComparisonTests
    {
        [SetUp]
        public void SetUp()
        {
            DynamicType.prefix = null;
        }

        [Test]
        public void ShouldFindNoDifferences()
        {
            var obj = DynamicType.CreateClass("C", DynamicType.CreateClass("B", DynamicType.CreateClass("A"))).Instantiate();
            var typeDescriptor = ((TypeFullDescriptor)obj.GetType());

            var compareResult = typeDescriptor.CompareWith(typeDescriptor);
            Assert.IsTrue(compareResult.Empty);
        }

        [Test]
        public void ShouldDetectFieldInsertionSimple()
        {
            var objPrev = DynamicType.CreateClass("A").Instantiate();
            var objCurr = DynamicType.CreateClass("A").WithField("a", typeof(int)).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsRemoved);
            Assert.IsEmpty(compareResult.FieldsChanged);

            Assert.AreEqual(1, compareResult.FieldsAdded.Count);
            Assert.AreEqual("A:a", compareResult.FieldsAdded[0].FullName);
        }

        [Test]
        public void ShouldNotDetectInsertionOfTransientField()
        {
            var objPrev = DynamicType.CreateClass("A").Instantiate();
            var objCurr = DynamicType.CreateClass("A").WithTransientField("a", typeof(int)).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsTrue(compareResult.Empty);
        }

        [Test]
        public void ShouldDetectInsertionOfOverridingField()
        {
            var objPrev = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).Instantiate();
            var objCurr = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).WithField("a", typeof(int)).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsRemoved);
            Assert.IsEmpty(compareResult.FieldsChanged);

            Assert.AreEqual(1, compareResult.FieldsAdded.Count);
            Assert.AreEqual("A:a", compareResult.FieldsAdded[0].FullName);
        }

        [Test]
        public void ShouldDetectFieldRemovalSimple()
        {
            var objPrev = DynamicType.CreateClass("A").WithField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A").Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsAdded);
            Assert.IsEmpty(compareResult.FieldsChanged);

            Assert.AreEqual(1, compareResult.FieldsRemoved.Count);
            Assert.AreEqual("A:a", compareResult.FieldsRemoved[0].FullName);
        }

        [Test]
        public void ShouldNotDetectRemovalOfTransientField()
        {
            var objPrev = DynamicType.CreateClass("A").WithTransientField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A").Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsTrue(compareResult.Empty);
        }

        [Test]
        public void ShouldDetectRemovalOfOverridingField()
        {
            var objPrev = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).WithField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsAdded);
            Assert.IsEmpty(compareResult.FieldsChanged);

            Assert.AreEqual(1, compareResult.FieldsRemoved.Count);
            Assert.AreEqual("A:a", compareResult.FieldsRemoved[0].FullName);
        }

        [Test]
        public void ShouldHandleFieldMoveDownSimple()
        {
            var objPrev = DynamicType.CreateClass("A", DynamicType.CreateClass("Base")).WithField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsChanged);

            Assert.AreEqual(1, compareResult.FieldsAdded.Count);
            Assert.AreEqual(1, compareResult.FieldsRemoved.Count);
            Assert.AreEqual("Base:a", compareResult.FieldsAdded.ElementAt(0).FullName);
            Assert.AreEqual("A:a", compareResult.FieldsRemoved.ElementAt(0).FullName);
        }

        [Test]
        public void ShouldHandleFieldMoveUpSimple()
        {
            var objPrev = DynamicType.CreateClass("A", DynamicType.CreateClass("Base")).WithField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsChanged);

            Assert.AreEqual(1, compareResult.FieldsAdded.Count);
            Assert.AreEqual(1, compareResult.FieldsRemoved.Count);
            Assert.AreEqual("A:a", compareResult.FieldsRemoved.ElementAt(0).FullName);
            Assert.AreEqual("Base:a", compareResult.FieldsAdded.ElementAt(0).FullName);
        }

        [Test]
        public void ShouldDetectFieldTypeChange()
        {
            var objPrev = DynamicType.CreateClass("A").WithField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A").WithField("a", typeof(long)).Instantiate();

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsEmpty(compareResult.FieldsAdded);
            Assert.IsEmpty(compareResult.FieldsRemoved);

            Assert.AreEqual(1, compareResult.FieldsChanged.Count);
            Assert.AreEqual("A:a", compareResult.FieldsChanged.ElementAt(0).FullName);
        }

        [Test]
        public void ShouldHandleAssemblyVersionChange()
        {
            var objPrev = DynamicType.CreateClass("A").WithField("a", DynamicType.CreateClass("C").WithField<int>("c")).Instantiate(new Version(1, 0));
            var objCurr = DynamicType.CreateClass("A").WithField("a", DynamicType.CreateClass("C").WithField<int>("c")).Instantiate(new Version(1, 1));

            var descPrev = ((TypeFullDescriptor)objPrev.GetType());
            var descCurr = ((TypeFullDescriptor)objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev, VersionToleranceLevel.AllowAssemblyVersionChange);

            Assert.IsEmpty(compareResult.FieldsAdded);
            Assert.IsEmpty(compareResult.FieldsChanged);
            Assert.IsEmpty(compareResult.FieldsRemoved);
        }
    }
}

