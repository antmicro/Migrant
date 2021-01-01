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
using System.Collections.Concurrent;

namespace Migrantoid.Tests
{
    [TestFixture(false, false, false, false, true)]
    [TestFixture(true,  false, false, false, true)]
    [TestFixture(false, true,  false, false, true)]
    [TestFixture(true,  true,  false, false, true)]
    [TestFixture(false, false, true,  false, true)]
    [TestFixture(true,  false, true,  false, true)]
    [TestFixture(false, true,  true,  false, true)]
    [TestFixture(true,  true,  true,  false, true)]
    [TestFixture(false, false, false, true,  true)]
    [TestFixture(true,  false, false, true,  true)]
    [TestFixture(false, true,  false, true,  true)]
    [TestFixture(true,  true,  false, true,  true)]
    [TestFixture(false, false, true,  true,  true)]
    [TestFixture(true,  false, true,  true,  true)]
    [TestFixture(false, true,  true,  true,  true)]
    [TestFixture(true,  true,  true,  true,  true)]
    [TestFixture(false, false, false, false, false)]
    [TestFixture(true,  false, false, false, false)]
    [TestFixture(false, true,  false, false, false)]
    [TestFixture(true,  true,  false, false, false)]
    [TestFixture(false, false, true,  false, false)]
    [TestFixture(true,  false, true,  false, false)]
    [TestFixture(false, true,  true,  false, false)]
    [TestFixture(true,  true,  true,  false, false)]
    [TestFixture(false, false, false, true,  false)]
    [TestFixture(true,  false, false, true,  false)]
    [TestFixture(false, true,  false, true,  false)]
    [TestFixture(true,  true,  false, true,  false)]
    [TestFixture(false, false, true,  true,  false)]
    [TestFixture(true,  false, true,  true,  false)]
    [TestFixture(false, true,  true,  true,  false)]
    [TestFixture(true,  true,  true,  true,  false)]
    public class BlockingCollectionSerializationTests : BaseTestWithSettings
    {
        public BlockingCollectionSerializationTests(
            bool useGeneratedSerializer,
            bool useGeneratedDeserializer,
            bool supportForISerializable,
            bool supportForIXmlSerializable,
            bool useTypeStamping) : 
        base(useGeneratedSerializer, useGeneratedDeserializer, false, supportForISerializable, supportForIXmlSerializable, useTypeStamping, true)
        {
        }

        [Test]
        public void ShouldSerializeBlockingCollectionWithPrimitives()
        {
            var collection = new BlockingCollection<int> { 1, 2, 3 };
            var copy = SerializerClone(collection);
            CollectionAssert.AreEquivalent(collection, copy);
        }

        [Test]
        public void ShouldSerializeBlockingCollectionWithNull()
        {
            var collection = new BlockingCollection<object> { 1, null, 3 };
            var copy = SerializerClone(collection);
            CollectionAssert.AreEquivalent(collection, copy);
        }

        [Test]
        public void ShouldSerializeBlockingCollectionWithStrings()
        {
            var collection = new BlockingCollection<string> { "One", "Two", "Three" };
            var copy = SerializerClone(collection);
            CollectionAssert.AreEquivalent(collection, copy);
        }
    }
}

