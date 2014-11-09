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
        public void ShouldDoBasicOpenStreamSerialization()
        {
            var someObject = new object();
            var otherObject = new object();

            var serializer = new Serializer(settings);
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
                    objs[i] = osDeserializer.Deserialize<object>();
                }
                Assert.AreEqual(objs[0], objs[1]);
                Assert.AreNotEqual(objs[1], objs[2]);
            }
        }

        private readonly Settings settings;
    }
}

