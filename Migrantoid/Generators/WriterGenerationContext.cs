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
using System.Reflection.Emit;

namespace Migrantoid.Generators
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

