//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using NUnit.Framework;
using Antmicro.Migrant.Customization;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Migrant.Tests
{
    [TestFixture(false, false)]
    [TestFixture(true, false)]
    [TestFixture(false, true)]
    [TestFixture(true, true)]
    public class OpenStreamSerializationTests
    {
        public OpenStreamSerializationTests(bool useGeneratedSerializer, bool useGeneratedDeserializer)
        {
            settings = new Settings(useGeneratedSerializer ? Method.Generated : Method.Reflection,
                useGeneratedDeserializer ? Method.Generated : Method.Reflection);
        }

        [Test]
        public void ShouldDoBasicOpenStreamSerialization(
            [Values(ReferencePreservation.DoNotPreserve, ReferencePreservation.Preserve, ReferencePreservation.UseWeakReference)]
            ReferencePreservation referencePreservation)
        {
            var localSettings = settings.With(referencePreservation: referencePreservation);

            var someObject = Tuple.Create(0, 1);
            var otherObject = Tuple.Create(1, 2);

            var serializer = new Serializer(localSettings);
            var stream = new MemoryStream();
            using(var osSerializer = serializer.ObtainOpenStreamSerializer(stream))
            {
                osSerializer.Serialize(someObject);
                osSerializer.Serialize(someObject);
                osSerializer.Serialize(otherObject);
            }

            stream.Seek(0, SeekOrigin.Begin);

            using(var osDeserializer = serializer.ObtainOpenStreamDeserializer(stream))
            {
                var objs = new object[3];
                for(var i = 0; i < objs.Length; i++)
                {
                    objs[i] = osDeserializer.Deserialize<Tuple<int, int>>();
                }
                Assert.AreEqual(someObject, objs[0]);
                Assert.AreEqual(someObject, objs[1]);
                Assert.AreEqual(otherObject, objs[2]);
            }
        }

        [Test]
        public void ShouldPreserveReferences(
            [Values(ReferencePreservation.Preserve, ReferencePreservation.UseWeakReference)]
            ReferencePreservation referencePreservation)
        {
            var localSettings = settings.With(referencePreservation: referencePreservation);

            var someObject = Tuple.Create(0, 1);
            var otherObject = Tuple.Create(1, 2);

            var serializer = new Serializer(localSettings);
            var stream = new MemoryStream();
            using(var osSerializer = serializer.ObtainOpenStreamSerializer(stream))
            {
                osSerializer.Serialize(someObject);
                osSerializer.Serialize(someObject);
                osSerializer.Serialize(otherObject);
            }

            stream.Seek(0, SeekOrigin.Begin);

            using(var osDeserializer = serializer.ObtainOpenStreamDeserializer(stream))
            {
                var objs = new object[3];
                for(var i = 0; i < objs.Length; i++)
                {
                    objs[i] = osDeserializer.Deserialize<Tuple<int, int>>();
                }
                Assert.AreSame(objs[0], objs[1]);
                Assert.AreNotSame(objs[1], objs[2]);
            }
        }

        [Test]
        public void ShouldntPreserveReferences()
        {
            var localSettings = settings.With(referencePreservation: ReferencePreservation.DoNotPreserve);

            var someObject = new object();

            var serializer = new Serializer(localSettings);
            var stream = new MemoryStream();
            using(var osSerializer = serializer.ObtainOpenStreamSerializer(stream))
            {
                osSerializer.Serialize(someObject);
                osSerializer.Serialize(someObject);
            }

            stream.Seek(0, SeekOrigin.Begin);

            using(var osDeserializer = serializer.ObtainOpenStreamDeserializer(stream))
            {
                Assert.AreNotSame(osDeserializer.Deserialize<object>(), osDeserializer.Deserialize<object>());
            }
        }

        [Test]
        public void ShouldDeserializeMany()
        {
            var serializer = new Serializer(settings.With(useBuffering: false));
            var stream = new MemoryStream();

            var tuples = Enumerable.Range(1, 10).Select(x => Tuple.Create(x, x + 0.5)).ToList();
            using(var osSerializer = serializer.ObtainOpenStreamSerializer(stream))
            {
                foreach(var tuple in tuples)
                {
                    osSerializer.Serialize(tuple);
                }
            }

            stream.Seek(0, SeekOrigin.Begin);

            using(var osDeserializer = serializer.ObtainOpenStreamDeserializer(stream))
            {
                var deserializedTuples = osDeserializer.DeserializeMany<Tuple<int, double>>();
                CollectionAssert.AreEqual(tuples.ToList(), deserializedTuples.ToList());
            }
        }

        private readonly Settings settings;
    }
}

