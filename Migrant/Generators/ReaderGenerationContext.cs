//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.Generators
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

