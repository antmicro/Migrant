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
    [TestFixture(true, true)]
    [TestFixture(true, false)]
    [TestFixture(true, true)]
    [TestFixture(false, false)]
    public class StampingTests
    {
        public StampingTests(bool serializeWithStamps, bool deserializeWithStamps)
        {
            this.serializeWithStamps = serializeWithStamps;
            this.deserializeWithStamps = deserializeWithStamps;
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
            return new Serializer(new Settings(disableTypeStamping: !serializeWithStamps));
        }

        private Serializer CreateDeserializer()
        {
            return new Serializer(new Settings(disableTypeStamping: !deserializeWithStamps));
        }

        private readonly bool serializeWithStamps;
        private readonly bool deserializeWithStamps;

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

