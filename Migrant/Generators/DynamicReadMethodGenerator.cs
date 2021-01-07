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

