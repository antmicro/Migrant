// *******************************************************************
//
//  Copyright (c) 2012-2016, Antmicro Ltd <antmicro.com>
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
using System.Reflection.Emit;
using Migrantoid.Utilities;

namespace Migrantoid.Generators
{
    internal class ReaderGenerationContext : GenerationContextBase
    {
        public ReaderGenerationContext(ILGenerator generator, bool disableStamping, bool treatCollectionAsUserObject, OpCode? objectIdArgument = null, OpCode? objectReaderArgument = null) : base(generator, disableStamping, treatCollectionAsUserObject)
        {
            this.objectIdArgument = objectIdArgument;
            this.objectReaderArgument = objectReaderArgument;
        }

        public void PushObjectIdOntoStack()
        {
            CheckAndEmit(objectIdArgument);
        }

        public void PushObjectReaderOntoStack()
        {
            CheckAndEmit(objectReaderArgument);
        }

        public void PushDeserializedObjectsCollectionOntoStack()
        {
            PushObjectReaderOntoStack();
            generator.PushFieldValueOntoStack<ObjectReader, AutoResizingList<object>>(x => x.deserializedObjects);
        }

        public void PushPrimitiveReaderOntoStack()
        {
            PushObjectReaderOntoStack();
            generator.PushPropertyValueOntoStack<ObjectReader, PrimitiveReader>(x => x.PrimitiveReader);
        }

        public void PushDeserializedObjectOntoStack(LocalBuilder referenceIdLocal, bool skipSurrogate = false)
        {
            PushDeserializedObjectOntoStack(() => generator.PushLocalValueOntoStack(referenceIdLocal), skipSurrogate);
        }

        public void PushDeserializedObjectOntoStack(Action pushReferenceIdOntoStackAction, bool skipSurrogate = false)
        {
            PushObjectReaderOntoStack();
            pushReferenceIdOntoStackAction();
            generator.PushIntegerOntoStack(skipSurrogate ? 1 : 0);
            generator.Call<ObjectReader>(x => x.GetObjectByReferenceId(0, false));
        }

        public override GenerationContextBase WithGenerator(ILGenerator g)
        {
            return new ReaderGenerationContext(g, DisableStamping, TreatCollectionAsUserObject, objectIdArgument, objectReaderArgument);
        }

        private readonly OpCode? objectIdArgument;
        private readonly OpCode? objectReaderArgument;
    }
}

