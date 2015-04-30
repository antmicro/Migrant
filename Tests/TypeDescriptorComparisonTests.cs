// *******************************************************************
//
//  Copyright (c) 2011-2015, Antmicro Ltd <antmicro.com>
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
using System.Linq;
using Antmicro.Migrant.Customization;

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
            var typeDescriptor = TypeDescriptor.CreateFromType(obj.GetType());

            var compareResult = typeDescriptor.CompareWith(typeDescriptor);
            Assert.IsTrue(compareResult.Empty);
        }

        [Test]
        public void ShouldDetectFieldInsertionSimple()
        {
            var objPrev = DynamicType.CreateClass("A").Instantiate();
            var objCurr = DynamicType.CreateClass("A").WithField("a", typeof(int)).Instantiate();

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsTrue(compareResult.Empty);
        }

        [Test]
        public void ShouldDetectInsertionOfOverridingField()
        {
            var objPrev = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).Instantiate();
            var objCurr = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).WithField("a", typeof(int)).Instantiate();

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev);

            Assert.IsTrue(compareResult.Empty);
        }

        [Test]
        public void ShouldDetectRemovalOfOverridingField()
        {
            var objPrev = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).WithField("a", typeof(int)).Instantiate();
            var objCurr = DynamicType.CreateClass("A", DynamicType.CreateClass("Base").WithField("a", typeof(int))).Instantiate();

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

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

            var descPrev = TypeDescriptor.CreateFromType(objPrev.GetType());
            var descCurr = TypeDescriptor.CreateFromType(objCurr.GetType());

            var compareResult = descCurr.CompareWith(descPrev, VersionToleranceLevel.AllowAssemblyVersionChange);

            Assert.IsEmpty(compareResult.FieldsAdded);
            Assert.IsEmpty(compareResult.FieldsChanged);
            Assert.IsEmpty(compareResult.FieldsRemoved);
        }
    }
}

