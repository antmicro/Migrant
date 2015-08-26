/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using NUnit.Framework;
using System.IO;
using Antmicro.Migrant;
using System.Collections.Generic;
using System.Reflection;

namespace Antmicro.Migrant.Tests
{
    [TestFixture(false, false, true)]
    [TestFixture(true, false, true)]
    [TestFixture(false, true, true)]
    [TestFixture(true, true, true)]
    [TestFixture(false, false, false)]
    [TestFixture(true, false, false)]
    [TestFixture(false, true, false)]
    [TestFixture(true, true, false)]
    public class SurrogateTests : BaseTestWithSettings
    {
        public SurrogateTests(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useTypeStamping) : base(useGeneratedSerializer, useGeneratedDeserializer, false, false, false, useTypeStamping)
		{

		}

		[Test]
		public void ShouldPlaceObjectForSurrogate()
		{
			var b = new SurrogateMockB();
			var pseudocopy = PseudoClone(b, serializer =>
			{
				serializer.ForSurrogate<SurrogateMockB>().SetObject(x => new SurrogateMockA(999));
			});
			var a = pseudocopy as SurrogateMockA;
			Assert.IsNotNull(a);
			Assert.AreEqual(999, a.Field);
		}

		[Test]
		public void ShouldPlaceObjectForSurrogatePreservingIdentity()
		{
			var b = new SurrogateMockB();
			var list = new List<object> { b, new List<object> { b }, new SurrogateMockB() };
			var counter = 0;
			var pseudocopy = PseudoClone(list, serializer =>
			{
				serializer.ForSurrogate<SurrogateMockB>().SetObject(x => new SurrogateMockA(counter++));
			});
			list = pseudocopy as List<object>;
			Assert.IsNotNull(list);
			var sublist = list[1] as List<object>;
			Assert.IsNotNull(sublist);
			Assert.AreSame(list[0], sublist[0]);
			Assert.AreNotSame(list[0], list[2]);
			var a = list[0] as SurrogateMockA;
			Assert.IsNotNull(a);
			Assert.AreEqual(counter - 2, a.Field);
			var secondA = list[2] as SurrogateMockA;
			Assert.IsNotNull(secondA);
			Assert.AreEqual(counter - 1, secondA.Field);
		}

		[Test]
		public void ShouldPlaceSurrogateForObject()
		{
			var b = new SurrogateMockB();
			var pseudocopy = PseudoClone(b, serializer =>
			{
				serializer.ForObject<SurrogateMockB>().SetSurrogate(x => new SurrogateMockA(1));
			});
			var a = pseudocopy as SurrogateMockA;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Field);
		}

		[Test]
		public void ShouldPlaceSurrogateForObjectPreservingIdentity()
		{
			var b = new SurrogateMockB();
			var counter = 0;
			var list = new List<object> { b, new SurrogateMockB(), b };
			var pseudocopy = PseudoClone(list, serializer =>
			{
				serializer.ForObject<SurrogateMockB>().SetSurrogate(x => new SurrogateMockA(counter++));
			});
			list = pseudocopy as List<object>;
			Assert.IsNotNull(list);
			Assert.AreSame(list[0], list[2]);
			Assert.AreNotSame(list[0], list[1]);
			var a = list[0] as SurrogateMockA;
			Assert.IsNotNull(a);
			Assert.AreEqual(counter - 2, a.Field);
			var secondA = list[1] as SurrogateMockA;
			Assert.IsNotNull(secondA);
			Assert.AreEqual(counter - 1, secondA.Field);
		}

		[Test]
		public void ShouldDoSurrogateObjectSwap()
		{
			var b = new SurrogateMockB();
			var pseudocopy = PseudoClone(b, serializer =>
			{
				serializer.ForObject<SurrogateMockB>().SetSurrogate(x => new SurrogateMockA(1));
				serializer.ForSurrogate<SurrogateMockA>().SetObject(x => new SurrogateMockC());
			});
			var c = pseudocopy as SurrogateMockC;
			Assert.IsNotNull(c);
		}

		[Test]
		public void ShouldPlaceObjectForDerivedSurrogate()
		{
			var d = new SurrogateMockD();
			var pseudocopy = PseudoClone(d, serializer =>
			{
				serializer.ForSurrogate<SurrogateMockC>().SetObject(x => new SurrogateMockB());
			});
			var b = pseudocopy as SurrogateMockB;
			Assert.IsNotNull(b);
		}

		[Test]
		public void ShouldPlaceSurrogateForDerivedObject()
		{
			var d = new SurrogateMockD();
			var pseudocopy = PseudoClone(d, serializer =>
			{
				serializer.ForObject<SurrogateMockC>().SetSurrogate(x => new SurrogateMockB());
			});
			var b = pseudocopy as SurrogateMockB;
			Assert.IsNotNull(b);
		}

		[Test]
		public void ShouldPlaceObjectForSurrogateImplementingInterface()
		{
			var e = new SurrogateMockE();
			var pseudocopy = PseudoClone(e, serializer =>
			{
				serializer.ForSurrogate<ISurrogateMockE>().SetObject(x => new SurrogateMockB());
			});
			var b = pseudocopy as SurrogateMockB;
			Assert.IsNotNull(b);
		}

		[Test]
		public void ShouldPlaceSurrogateForObjectImplementingInterface()
		{
			var e = new SurrogateMockE();
			var pseudocopy = PseudoClone(e, serializer =>
			{
				serializer.ForObject<ISurrogateMockE>().SetSurrogate(x => new SurrogateMockB());
			});
			var b = pseudocopy as SurrogateMockB;
			Assert.IsNotNull(b);
		}

        [Test]
        public void ShouldUseMoreSpecificSurrogateIfPossible()
        {
            var mock = new SurrogateMockD();
            var pseudocopy = PseudoClone(mock, serializer =>
            {
                serializer.ForObject<SurrogateMockC>().SetSurrogate(x => new SurrogateMockA(1));
                serializer.ForObject<SurrogateMockD>().SetSurrogate(x => new SurrogateMockB());
            });
            var b = pseudocopy as SurrogateMockB;
            Assert.IsNotNull(b);
        }

		[Test]
		public void ShouldThrowWhenSettingSurrogatesAfterSerialization()
		{
            var serializer = new Serializer(GetSettings());
			serializer.Serialize(new object(), Stream.Null);
			Assert.Throws<InvalidOperationException>(() => serializer.ForObject<object>().SetSurrogate(x => new object()));
		}

		[Test]
		public void ShouldThrowWhenSettingObjectForSurrogateAfterDeserialization()
		{
            var serializer = new Serializer(GetSettings());
			var stream = new MemoryStream();
			serializer.Serialize(new object(), stream);
			stream.Seek(0, SeekOrigin.Begin);
			serializer.Deserialize<object>(stream);
			Assert.Throws<InvalidOperationException>(() => serializer.ForSurrogate<object>().SetObject(x => new object()));
		}

        [Test]
        public void ShouldDoSurrogateObjectSwapTwoTimes()
        {
            var b = new SurrogateMockB();
            var serializer = new Serializer(GetSettings());
            serializer.ForObject<SurrogateMockB>().SetSurrogate(x => new SurrogateMockA(1));
            serializer.ForSurrogate<SurrogateMockA>().SetObject(x => new SurrogateMockC());

            for(var i = 0; i < 2; i++)
            {
                using(var stream = new MemoryStream())
                {
                    serializer.Serialize(b, stream);
                    stream.Seek(0, SeekOrigin.Begin);
                    var pseudocopy = serializer.Deserialize<object>(stream);
                    var c = pseudocopy as SurrogateMockC;
                    Assert.IsNotNull(c);
                }
            }
        }

        [Test]
        public void ShouldUseSurrogateAddedFirstWhenBothMatch()
        {
            var h = new SurrogateMockH();
            var pseudocopy = PseudoClone(h, serializer =>
            {
                serializer.ForObject<ISurrogateMockG>().SetSurrogate(x => "g");
                serializer.ForObject<ISurrogateMockE>().SetSurrogate(x => "e");
            });
            Assert.AreEqual("g", pseudocopy);
        }

        [Test]
        public void ShouldSwapGenericSurrogateWithObject()
        {
            var f = new SurrogateMockF<string>("test");
            var pseudocopy = PseudoClone(f, serializer => serializer.ForObject(typeof(SurrogateMockF<>)).SetSurrogate(x =>
            {
                var type = x.GetType();
                return type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)[0].GetValue(x);
            }));
            Assert.IsInstanceOf<string>(pseudocopy);
        }

        [Test]
        public void ShouldSwapWithNonGenericSurrogate()
        {
            var f = new SurrogateMockF<string>("test");
            var pseudocopy = PseudoClone(f, serializer =>
            {
                serializer.ForObject(typeof(SurrogateMockF<>)).SetSurrogate(x => "general");
                serializer.ForObject<SurrogateMockF<string>>().SetSurrogate(x => "special");
            });
            Assert.AreEqual("special", pseudocopy);
        }

        [Test]
        public void ShouldUseGenericBaseSurrogateForDerivedClass()
        {
            var i = new SurrogateMockI<string>("something");
            var pseudocopy = PseudoClone(i, serializer =>
                serializer.ForObject(typeof(SurrogateMockF<>)).SetSurrogate(x => "success"));
            Assert.AreEqual("success", pseudocopy);            
        }

        [Test]
        public void ShouldUseMoreSpecificGenericSurrogateIfPossible()
        {
            var i = new SurrogateMockI<string>("something");
            var pseudocopy = PseudoClone(i, serializer =>
            {
                serializer.ForObject(typeof(SurrogateMockF<>)).SetSurrogate(x => "fail");
                serializer.ForObject(typeof(SurrogateMockI<>)).SetSurrogate(x => "success");
            });
            Assert.AreEqual("success", pseudocopy);
        }

        [Test]
        public void ShouldTreatNullAsDontSurrogateThisType()
        {
            var d = new SurrogateMockD();
            var pseudocopy = PseudoClone(d, serializer =>
            {
                serializer.ForObject(typeof(SurrogateMockC)).SetSurrogate(x => "fail");
                serializer.ForObject(typeof(SurrogateMockD)).SetSurrogate(null);
            });
            Assert.IsInstanceOf<SurrogateMockD>(pseudocopy);
        }
	}

	public class SurrogateMockA
	{
		public SurrogateMockA(int field)
		{
			Field = field;
		}

		public int Field { get; private set; }
	}

	public class SurrogateMockB
	{

	}

	public class SurrogateMockC
	{

	}

	public class SurrogateMockD : SurrogateMockC
	{

	}

	public interface ISurrogateMockE
	{

	}

	public class SurrogateMockE : ISurrogateMockE
	{

	}

    public class SurrogateMockF<T>
    {
        public SurrogateMockF(T value)
        {
            Value = value;
        }

        public T Value { get; private set; }
    }

    public interface ISurrogateMockG
    {
        
    }

    public class SurrogateMockH : ISurrogateMockE, ISurrogateMockG
    {
        
    }

    public class SurrogateMockI<T> : SurrogateMockF<T>
    {
        public SurrogateMockI(T value) : base(value)
        {
        }        
    }
}

