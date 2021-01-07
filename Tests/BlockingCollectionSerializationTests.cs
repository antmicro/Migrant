//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace Antmicro.Migrant.Tests
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

