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
using Antmicro.Migrant.Customization;
using System.IO;

namespace Antmicro.Migrant.Tests
{
    [TestFixture(false, false, true, true)]
    [TestFixture(false, false, true, false)]
    [TestFixture(false, false, false, true)]
    [TestFixture(false, false, false, false)]
    [TestFixture(false, true, true, true)]
    [TestFixture(false, true, true, false)]
    [TestFixture(false, true, false, true)]
    [TestFixture(false, true, false, false)]
    [TestFixture(true, false, true, true)]
    [TestFixture(true, false, true, false)]
    [TestFixture(true, false, false, true)]
    [TestFixture(true, false, false, false)]
    [TestFixture(true, true, true, true)]
    [TestFixture(true, true, true, false)]
    [TestFixture(true, true, false, true)]
    [TestFixture(true, true, false, false)]
    public class StampingTests
    {
        public StampingTests(bool useGeneratedSerialization, bool useGeneratedDeserialization, bool serializeWithStamps, bool deserializeWithStamps)
        {
            this.serializeWithStamps = serializeWithStamps;
            this.deserializeWithStamps = deserializeWithStamps;
            this.useGeneratedSerialization = useGeneratedSerialization;
            this.useGeneratedDeserialization = useGeneratedDeserialization;
        }

        [Test]
        public void ShouldDetectStreamInconsistentConfiguration()
        {
            var obj = new TestClass();
            var mstream = new MemoryStream();
            var serializer = CreateSerializer();
            serializer.Serialize(obj, mstream);
            mstream.Seek(0, SeekOrigin.Begin);
            var deserializer = CreateDeserializer();
            try
            {
                deserializer.Deserialize<TestClass>(mstream);
                if(serializeWithStamps != deserializeWithStamps)
                {
                    Assert.Fail("Should not deserialize when stream configuration is inconsistent.");
                }
            }
            catch(Exception e)
            {
                if(serializeWithStamps == deserializeWithStamps)
                {
                    Console.WriteLine(e.Message);
                    Assert.Fail("Should deserialize when stream configuration is correct.");
                }
            }
        }

        private Serializer CreateSerializer()
        {
            return new Serializer(new Settings(
                useGeneratedSerialization ? Method.Generated : Method.Reflection,
                disableTypeStamping: !serializeWithStamps));
        }

        private Serializer CreateDeserializer()
        {
            return new Serializer(new Settings(
                deserializationMethod: useGeneratedDeserialization ? Method.Generated : Method.Reflection,
                disableTypeStamping: !deserializeWithStamps));
        }

        private readonly bool serializeWithStamps;
        private readonly bool deserializeWithStamps;
        private readonly bool useGeneratedSerialization;
        private readonly bool useGeneratedDeserialization;

        private class TestClass
        {
            public TestClass()
            {
                number = DateTime.Now.Millisecond;
                obj = this;
            }

            #pragma warning disable 414
            private readonly int number;
            private readonly TestClass obj;
            #pragma warning restore 414
        }
    }
}

