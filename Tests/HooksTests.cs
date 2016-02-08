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
using Antmicro.Migrant.Hooks;
using NUnit.Framework;
using System.IO;
using System;
using System.Threading;
using System.Collections.Generic;

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
    public class HooksTests : BaseTestWithSettings
    {
        public HooksTests(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useTypeStamping) : base(useGeneratedSerializer, useGeneratedDeserializer, false, false, false, useTypeStamping)
        {
        }

        [Test]
        public void ShouldInvokeChainOfHooksInCorrectOrder()
        {
            ClassWithTheReference.Reset();
            var array = new ClassWithTheReference[6];
            for(var i = 0; i < array.Length; i++)
            {
                array[i] = new ClassWithTheReference(i);
            }

            array[0].refOne = array[1];
            array[0].refTwo = array[2];
            array[1].refOne = array[3];
            array[1].refTwo = array[4];
            array[2].refOne = array[1];
            array[2].refTwo = array[5];

            var copy = SerializerClone(array);

            Assert.AreEqual(1, copy[3].value);
            Assert.AreEqual(2, copy[4].value);
            Assert.AreEqual(3, copy[1].value);
            Assert.AreEqual(4, copy[5].value);
            Assert.AreEqual(5, copy[2].value);
            Assert.AreEqual(6, copy[0].value);
        }

        private class ClassWithTheReference
        {
            public ClassWithTheReference(int id)
            {
                this.id = id;
            }

            // we don't use array/list here to avoid additional objects
            public ClassWithTheReference refOne;
            public ClassWithTheReference refTwo;

            public int value;

            public int id;

            [PostDeserialization]
            private void PostDeserializationMethod()
            {
                value = ++counter;
            }

            public static void Reset()
            {
                counter = 0;
            }

            private static int counter = 0;
        }

        [Test]
        public void ShouldInvokePreSerialization()
        {
            var mock = new PreSerializationMock();
            var copy = SerializerClone(mock);
            Assert.IsTrue(mock.Invoked);
            Assert.IsTrue(copy.Invoked);
        }

        [Test]
        public void ShouldInvokeDerivedPreSerialization()
        {
            var mock = new PreSerializationMockDerived();
            var copy = SerializerClone(mock);
            Assert.IsTrue(mock.Invoked);
            Assert.IsTrue(mock.DerivedInvoked);
            Assert.IsTrue(copy.Invoked);
            Assert.IsTrue(copy.DerivedInvoked);
        }

        [Test]
        public void ShouldInvokePostSerialization()
        {
            var mock = new ImmediatePostSerializationMock();
            var copy = SerializerClone(mock);
            Assert.IsTrue(mock.Invoked);
            Assert.IsFalse(copy.Invoked);
        }

        [Test]
        public void ShouldInvokeDerivedPostSerialization()
        {
            var mock = new ImmediatePostSerializationMockDerived();
            var copy = SerializerClone(mock);
            Assert.IsTrue(mock.Invoked);
            Assert.IsTrue(mock.DerivedInvoked);
            Assert.IsFalse(copy.Invoked);
            Assert.IsFalse(copy.DerivedInvoked);
        }

        [Test]
        public void ShouldInvokeStaticPostSerialization()
        {
            var mock = new StaticImmediatePostSerializationMock();
            SerializerClone(mock);
            Assert.IsTrue(StaticImmediatePostSerializationMock.Invoked);
        }

        [Test]
        public void ShouldInvokePostDeserialization()
        {
            var mock = new PostDeserializationMock();
            var copy = SerializerClone(mock);
            Assert.IsFalse(mock.Invoked);
            Assert.IsTrue(copy.Invoked);
        }

        [Test]
        public void ShouldInvokeDerivedPostDeserialization()
        {
            var mock = new PostDeserializationMockDerived();
            var copy = SerializerClone(mock);
            Assert.IsFalse(mock.Invoked);
            Assert.IsFalse(mock.DerivedInvoked);
            Assert.IsTrue(copy.Invoked);
            Assert.IsTrue(copy.DerivedInvoked);
        }

        [Test]
        public void ShouldInvokeGlobalHooks()
        {
            var memoryStream = new MemoryStream();
            var serializer = new Serializer(GetSettings());
            var preSerializationCounter = 0;
            var postSerializationCounter = 0;
            var postDeserializationCounter = 0;
            serializer.OnPreSerialization += x => preSerializationCounter++;
            serializer.OnPostSerialization += x => postSerializationCounter++;
            serializer.OnPostDeserialization += x => postDeserializationCounter++;

            serializer.Serialize(new [] { "One", "Two" }, memoryStream);
            Assert.AreEqual(3, preSerializationCounter);
            Assert.AreEqual(3, postSerializationCounter);
            Assert.AreEqual(0, postDeserializationCounter);

            memoryStream.Seek(0, SeekOrigin.Begin);
            serializer.Deserialize<string[]>(memoryStream);
            Assert.AreEqual(3, postDeserializationCounter);
        }

        [Test]
        public void ShouldHaveDeserializedReferencedObjectsWhenHookIsInvoked()
        {
            var referencing = new ReferencingPostDeserializationMock();
            SerializerClone(referencing);
        }

        [Test]
        public void ShouldInvokePostHookAfterBothObjectsInCyclicReferenceAreDeserialized()
        {
            var a = new CyclicReferenceMockA();
            a.B = new CyclicReferenceMockB();
            a.B.A = a;
            SerializerClone(a);
        }

        [Test]
        public void ShouldInvokeLatePostSerializationHookAfterImmediate()
        {
            var late = new LatePostSerializationMockA();
            SerializerClone(late);
        }

        [Test]
        public void ShouldInvokeLatePostDeserializationHookAfterImmediate()
        {
            var late = new LatePostSerializationMockA();
            SerializerClone(late);
        }

        [Test]
        public void ShouldInvokeImmediateHooksInCorrectOrder()
        {
            var forOrderTest = new ForOrderTestA { B = new ForOrderTestB() };
            var copy = SerializerClone(forOrderTest);
            Assert.Less(forOrderTest.B.HookInvokedOn, forOrderTest.HookInvokedOn);
            Assert.Less(copy.B.HookInvokedOn, copy.HookInvokedOn);
        }

        [Test]
        public void ShouldInvokeLateHooksInCorrectOrder()
        {
            var forLateOrderTest = new ForLateOrderTestA { B = new ForLateOrderTestB() };
            var copy = SerializerClone(forLateOrderTest);
            Assert.Less(forLateOrderTest.B.HookInvokedOn, forLateOrderTest.HookInvokedOn);
            Assert.Less(copy.B.HookInvokedOn, copy.HookInvokedOn);
        }

        [Test]
        public void ShouldInvokePostSerializationEvenIfExceptionWasThrownDuringSerializationEarly()
        {
            ShouldInvokePostDeserializationEvenIfExceptionWasThrownDuringSerialization(new PrePostSerializationMock());
        }

        [Test]
        public void ShouldInvokePostSerializationEvenIfExceptionWasThrownDuringSerializationLate()
        {
            ShouldInvokePostDeserializationEvenIfExceptionWasThrownDuringSerialization(new LatePrePostSerializationMock());
        }

        [Test]
        public void ShouldInvokeHooksOnDerivedClassesInCorrectOrder()
        {
            var obj = new DerivedFromClassB();
            var copy = SerializerClone(obj);
            Assert.IsTrue(copy.FlagC);
        }

        [Test]
        public void ShouldProperlyExecuteHooksOnVirtualMethod()
        {
            var mockWithVirtual = new MockWithVirtualDerived();
            var copy = SerializerClone(mockWithVirtual);
            Assert.AreEqual(1, mockWithVirtual.BasePreSerializationCounter);
            Assert.AreEqual(2, mockWithVirtual.DerivedPreSerializationCounter);
            Assert.AreEqual(3, mockWithVirtual.BasePostSerializationCounter);
            Assert.AreEqual(4, mockWithVirtual.DerivedPostSerializationCounter);
            Assert.AreEqual(3, copy.BasePostDeserializationCounter);
            Assert.AreEqual(4, copy.DerivedPostDeserializationCounter);
        }

        [Test]
        public void ShouldFailWithSurrogateAndLatePostDeserializationHook()
        {
            try
            {
                var lateHook = new LateDeserializationMockA();
                PseudoClone(lateHook, serializer => serializer.ForSurrogate<LateDeserializationMockA>().SetObject(x => new object()));
                Assert.Fail("Serialization finished while it should fail.");
            }
            catch(InvalidOperationException)
            {

            }
        }

        private void ShouldInvokePostDeserializationEvenIfExceptionWasThrownDuringSerialization<T>(T prePostMock) where T : IPrePostMock
        {
            try
            {
                SerializerClone(prePostMock);
                Assert.Fail("The exception has not propagated.");
            }
            catch(InvalidOperationException)
            {

            }
            Assert.AreEqual(true, prePostMock.PreExecuted);
            Assert.AreEqual(true, prePostMock.PostExecuted);
        }
    }

    public class PreSerializationMock
    {
        [PreSerialization]
        private void PreSerialization()
        {
            Invoked = true;
        }

        public bool Invoked { get; private set; }
    }

    public class PreSerializationMockDerived : PreSerializationMock
    {
        [PreSerialization]
        private void PreSerializationDerived()
        {
            DerivedInvoked = true;
        }

        public bool DerivedInvoked { get; private set; }
    }

    public class ImmediatePostSerializationMock
    {
        [PostSerializationAttribute]
        private void PostSerialization()
        {
            Invoked = true;
        }

        public bool Invoked { get; private set; }
    }

    public class ImmediatePostSerializationMockDerived : ImmediatePostSerializationMock
    {
        [PostSerializationAttribute]
        private void PostSerializationDerived()
        {
            DerivedInvoked = true;
        }

        public bool DerivedInvoked { get; private set; }
    }

    public class StaticImmediatePostSerializationMock
    {
        [PostSerializationAttribute]
        private static void PostSerialization()
        {
            Invoked = true;
        }

        public static bool Invoked { get; private set; }
    }

    public class PostDeserializationMock
    {
        [LatePostDeserializationAttribute]
        private void PostDeserialization()
        {
            Invoked = true;
        }

        public bool Invoked { get; private set; }
    }

    public class PostDeserializationMockDerived : PostDeserializationMock
    {
        [LatePostDeserializationAttribute]
        private void PostDeserializationDerived()
        {
            DerivedInvoked = true;
        }

        public bool DerivedInvoked { get; private set; }
    }

    public class ReferencingPostDeserializationMock
    {
        public ReferencingPostDeserializationMock()
        {
            mock = new ReferencedPostDeserializationMock();
        }

        [LatePostDeserializationAttribute]
        private void PostDeserialization()
        {
            if(mock.TestObject == null)
            {
                throw new InvalidOperationException("Referenced mock was still not deserialized when invoking hook.");
            }
        }

        private readonly ReferencedPostDeserializationMock mock;
    }

    public class ReferencedPostDeserializationMock
    {
        public ReferencedPostDeserializationMock()
        {
            TestObject = new object();
        }

        public object TestObject { get; private set; }
    }

    public class CyclicReferenceMockA
    {
        public CyclicReferenceMockB B { get; set; }

        public string Str { get; set; }

        public CyclicReferenceMockA()
        {
            Str = "Something";
        }

        [LatePostDeserializationAttribute]
        public void TestIfBIsReady()
        {
            if(B == null || B.Str == null)
            {
                throw new InvalidOperationException("B is not ready after deserialization.");
            }
        }
    }

    public class CyclicReferenceMockB
    {
        public CyclicReferenceMockA A { get; set; }

        public string Str { get; set; }

        public CyclicReferenceMockB()
        {
            Str = "Something different";
        }

        [LatePostDeserializationAttribute]
        public void TestIfAIsReady()
        {
            if(A == null || A.Str == null)
            {
                throw new InvalidOperationException("A is not ready after deserialization.");
            }
        }
    }

    public class LatePostSerializationMockA
    {
        public LatePostSerializationMockA()
        {
            B = new PostSerializationMockB();
        }

        public PostSerializationMockB B { get; set; }

        [LatePostSerialization]
        private void PostSerialization()
        {
            if(!B.PostSerialized)
            {
                throw new InvalidOperationException("Late post serialization hook happened earlier than immediate one on referenced class.");
            }
        }
    }

    public class PostSerializationMockB
    {
        public bool PostSerialized { get; private set; }

        [PostSerializationAttribute]
        private void PostSerialization()
        {
            PostSerialized = true;
        }
    }

    public class LateDeserializationMockA
    {
        public LateDeserializationMockA()
        {
            B = new DeserializationMockB();
        }

        public DeserializationMockB B { get; set; }

        [LatePostDeserializationAttribute]
        private void PostDeserialization()
        {
            if(!B.PostDeserialized)
            {
                throw new InvalidOperationException("Late post serialization hook happened earlier than immediate one on referenced class.");
            }
        }
    }

    public class DeserializationMockB
    {
        public bool PostDeserialized { get; private set; }

        [PostDeserializationAttribute]
        private void PostDeserialization()
        {
            PostDeserialized = true;
        }
    }

    public class ForOrderTestA : ForOrderTestB
    {
        public ForOrderTestB B { get; set; }
    }

    public class ForOrderTestB
    {
        public DateTime HookInvokedOn { get; private set; }

        [PostDeserialization]
        [PostSerialization]
        public void AfterDeOrSerialization()
        {
            HookInvokedOn = DateTime.Now;
            Thread.Sleep(100);
        }
    }

    public class ForLateOrderTestA : ForLateOrderTestB
    {
        public ForLateOrderTestB B { get; set; }
    }

    public class ForLateOrderTestB
    {
        public DateTime HookInvokedOn { get; private set; }

        [LatePostDeserialization]
        [LatePostSerialization]
        public void AfterDeOrSerialization()
        {
            HookInvokedOn = DateTime.Now;
            Thread.Sleep(100);
        }
    }

    public class ClassSendingExcetpionDuringSerialization
    {
        [PreSerialization]
        private void SendException()
        {
            throw new InvalidOperationException();
        }
    }

    public interface IPrePostMock
    {
        bool PreExecuted { get; }

        bool PostExecuted { get; }
    }

    public class PrePostSerializationMock : IPrePostMock
    {
        public PrePostSerializationMock()
        {
            sendingException = new ClassSendingExcetpionDuringSerialization();
        }

        public bool PreExecuted { get; private set; }

        public bool PostExecuted { get; private set; }

        [PreSerialization]
        private void BeforeSerialization()
        {
            PreExecuted = true;
        }

        [PostSerialization]
        private void AfterSerialization()
        {
            PostExecuted = true;
        }

        #pragma warning disable 0414
        private ClassSendingExcetpionDuringSerialization sendingException;
        #pragma warning restore 0414
    }

    public class LatePrePostSerializationMock : IPrePostMock
    {
        public LatePrePostSerializationMock()
        {
            sendingException = new ClassSendingExcetpionDuringSerialization();
        }

        public bool PreExecuted { get; private set; }

        public bool PostExecuted { get; private set; }

        [PreSerialization]
        private void BeforeSerialization()
        {
            PreExecuted = true;
        }

        [LatePostSerialization]
        private void AfterSerialization()
        {
            PostExecuted = true;
        }

        #pragma warning disable 0414
        private ClassSendingExcetpionDuringSerialization sendingException;
        #pragma warning restore 0414
    }

    public class BaseClassA
    {
        [Transient]
        protected bool FlagA;

        [PostDeserialization]
        private void AfterDeserialization()
        {
            FlagA = true;
        }
    }

    public class DerivedFromBaseClassA : BaseClassA
    {
        [Transient]
        protected bool FlagB;

        [PostDeserialization]
        private void AfterDeserialization()
        {
            Assert.IsTrue(FlagA);
            FlagB = true;
        }
    }

    public class DerivedFromClassB : DerivedFromBaseClassA
    {
        [Transient]
        public bool FlagC;

        [PostDeserialization]
        private void AfterDeserialization()
        {
            Assert.IsTrue(FlagA);
            Assert.IsTrue(FlagB);
            FlagC = true;
        }
    }

    public class MockWithVirtualBase
    {
        public int BasePreSerializationCounter { get; private set; }
        public int BasePostSerializationCounter { get; private set; }
        public int BasePostDeserializationCounter { get; private set; }

        [PreSerialization]
        protected virtual void BeforeSerialization()
        {
            BasePreSerializationCounter = Counter;
        }

        [PostSerialization]
        protected virtual void AfterSerialization()
        {
            BasePostSerializationCounter = Counter;
        }

        [PostDeserialization]
        protected virtual void AfterDeserialization()
        {
            BasePostDeserializationCounter = Counter;
        }

        protected int Counter
        {
            get
            {
                return ++counterValue;
            }
        }

        private int counterValue;
    }

    public class MockWithVirtualDerived : MockWithVirtualBase
    {
        public int DerivedPreSerializationCounter { get; private set; }
        public int DerivedPostSerializationCounter { get; private set; }
        public int DerivedPostDeserializationCounter { get; private set; }

        protected override void BeforeSerialization()
        {
            base.BeforeSerialization();
            DerivedPreSerializationCounter = Counter;
        }

        protected override void AfterSerialization()
        {
            base.AfterSerialization();
            DerivedPostSerializationCounter = Counter;
        }

        protected override void AfterDeserialization()
        {
            base.AfterDeserialization();
            DerivedPostDeserializationCounter = Counter;
        }
    }
}

