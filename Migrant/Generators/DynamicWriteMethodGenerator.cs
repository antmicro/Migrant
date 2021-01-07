//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Antmicro.Migrant.Generators
{
    internal abstract class DynamicWriteMethodGenerator<T> : DynamicMethodGenerator<T> where T : class
    {
        public DynamicWriteMethodGenerator(Type type, string name, bool disableStamping, bool treatCollectionAsUserObject, OpCode objectWriterArgument)
            : base(type, disableStamping, treatCollectionAsUserObject)
        {
            this.name = name;
            this.objectWriterArgument = objectWriterArgument;
        }

        protected override MethodInfo GenerateInner()
        {
            var dynamicMethod = new DynamicMethod(name, returnType, parameterTypes, typeof(Serializer), true);
            var generator = dynamicMethod.GetILGenerator();
            var context = new WriterGenerationContext(generator, disableStamping, treatCollectionAsUserObject, objectWriterArgument);
            if(!InnerGenerate(context))
            {
                return null;
            }
            context.Generator.Emit(OpCodes.Ret);
#if DEBUG_FORMAT
            GeneratorHelper.DumpToLibrary<T>(context, x => InnerGenerate((WriterGenerationContext)x), name);
#endif
            return dynamicMethod;
        }

        protected abstract bool InnerGenerate(WriterGenerationContext context);

        private readonly string name;
        private readonly OpCode objectWriterArgument;
    }
}

