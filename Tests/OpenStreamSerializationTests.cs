// *******************************************************************
//
//  Copyright (c) 2011-2014, Antmicro Ltd <antmicro.com>
//
//  Authors:
//   * Konrad Kruczynski (kkruczynski@antmicro.com)
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

        [Test, Ignore]
        public void ShouldReadPreservationOptionFromTheStream(
            [Values(ReferencePreservation.Preserve, ReferencePreservation.DoNotPreserve)]
            ReferencePreservation referencePreservation)
        {
            var localSettings = settings.With(referencePreservation: referencePreservation);
            var oppositeSettings = settings.With(referencePreservation: 
                referencePreservation == ReferencePreservation.Preserve
                ? ReferencePreservation.DoNotPreserve : ReferencePreservation.Preserve);

            var someObject = Tuple.Create(0, 1);

            var serializer = new Serializer(localSettings);
            var stream = new MemoryStream();
            using(var osSerializer = serializer.ObtainOpenStreamSerializer(stream))
            {
                osSerializer.Serialize(someObject);
                osSerializer.Serialize(someObject);
            }

            stream.Seek(0, SeekOrigin.Begin);
            serializer = new Serializer(oppositeSettings);

            using(var osDeserializer = serializer.ObtainOpenStreamDeserializer(stream))
            {
                var first = osDeserializer.Deserialize<object>();
                var second = osDeserializer.Deserialize<object>();
                Assert.AreEqual(first, second);
                Assert.AreEqual(first, someObject);
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

