// *******************************************************************
//
//  Copyright (c) 2013, Antmicro Ltd
//  
//  Author:
//   Konrad Kruczyński (kkruczynski@antmicro.com)
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
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace AntMicro.Migrant.Tests
{
    [TestFixture(false, false)]
    [TestFixture(true, false)]
    [TestFixture(false, true)]
    [TestFixture(true, true)]
    public class BuiltinSurrogatesTests : BaseTestWithSettings
    {
        public BuiltinSurrogatesTests(bool useGeneratedSerializer, bool useGeneratedDeserializer) : base(useGeneratedSerializer, useGeneratedDeserializer)
        {

        }   

        [Test]
        public void ShouldSerializeRegex()
        {
            var regex = new Regex("[0-9]");
            var copy = SerializerClone(regex);
            Assert.AreEqual(regex.ToString(), copy.ToString());
        }

        [Test]
        public void ShouldSerializeSimpleCustomISerializable()
        {
            var value = 666L;
            var customSerializable = new CustomISerializable(value);
            var copy = SerializerClone(customSerializable);
            Assert.AreEqual(customSerializable, copy);
        }

        public sealed class CustomISerializable : ISerializable
        {
            public CustomISerializable(long value)
            {
                fakeIntPtr = new IntPtr(value);
            }

            private CustomISerializable(SerializationInfo info, StreamingContext context)
            {
                fakeIntPtr = new IntPtr(info.GetInt64("ValueOfThePointer"));
            }

            public long ValueAsLong
            {
                get
                {
                    return fakeIntPtr.ToInt64();
                }
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ValueOfThePointer", fakeIntPtr.ToInt64());
            }

            public override bool Equals(object obj)
            {
                if(obj == null)
                    return false;
                if(ReferenceEquals(this, obj))
                    return true;
                if(obj.GetType() != typeof(CustomISerializable))
                    return false;
                CustomISerializable other = (CustomISerializable)obj;
                return fakeIntPtr == other.fakeIntPtr;
            }            

            public override int GetHashCode()
            {
                unchecked
                {
                    return fakeIntPtr.GetHashCode();
                }
            }            

            private IntPtr fakeIntPtr;
        }
    }
}

