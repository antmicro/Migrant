//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.Reflection.Emit;

namespace Antmicro.Migrant.Generators
{
    internal class WriterGenerationContext : GenerationContextBase
    {
        public WriterGenerationContext(ILGenerator generator, bool disableStamping, bool treatCollectionAsUserObject, OpCode? objectWriterArgument = null) : base(generator, disableStamping, treatCollectionAsUserObject)
        {
            this.objectWriterArgument = objectWriterArgument;
        }

        public void PushObjectWriterOntoStack()
        {
            CheckAndEmit(objectWriterArgument);
        }

        public void PushPrimitiveWriterOntoStack()
        {
            PushObjectWriterOntoStack();
            generator.PushFieldValueOntoStack<ObjectWriter, PrimitiveWriter>(x => x.writer);
        }

        public void PushNullReferenceOnStack()
        {
            PushPrimitiveWriterOntoStack();
            generator.Emit(OpCodes.Ldc_I4, Consts.NullObjectId);
            generator.Call<PrimitiveWriter>(x => x.Write(0));
        }

        public override GenerationContextBase WithGenerator(ILGenerator g)
        {
            return new WriterGenerationContext(g, DisableStamping, TreatCollectionAsUserObject, objectWriterArgument);
        }

        private readonly OpCode? objectWriterArgument;
    }
}

