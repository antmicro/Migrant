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
using System.Reflection;
using System.Reflection.Emit;

namespace Antmicro.Migrant.Generators
{
    internal abstract class DynamicReadMethodGenerator<T> : DynamicMethodGenerator<T> where T : class
    {
        public DynamicReadMethodGenerator(Type type, string name, bool disableStamping = false, bool treatCollectionAsUserObject = false, OpCode? objectIdArgument = null, OpCode? objectReaderArgument = null)
            : base(type, disableStamping, treatCollectionAsUserObject)
        {
            this.name = name;
            this.objectIdArgument = objectIdArgument;
            this.objectReaderArgument = objectReaderArgument;
        }

        protected override MethodInfo GenerateInner()
        {
            var dynamicMethod = new DynamicMethod(name, returnType, parameterTypes, typeof(Serializer), true);
            var generator = dynamicMethod.GetILGenerator();
            var context = new ReaderGenerationContext(generator, disableStamping, treatCollectionAsUserObject, objectIdArgument, objectReaderArgument);
            InnerGenerate(context);
            context.Generator.Emit(OpCodes.Ret);
#if DEBUG_FORMAT
            GeneratorHelper.DumpToLibrary<T>(context, x => InnerGenerate((ReaderGenerationContext)x), name);
#endif
            return dynamicMethod;
        }

        protected abstract void InnerGenerate(ReaderGenerationContext context);

        private readonly string name;

        private readonly OpCode? objectReaderArgument;
        private readonly OpCode? objectIdArgument;
    }
}

