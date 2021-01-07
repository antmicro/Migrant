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
    internal class CreateGenericSurrogateMethodGenerator : DynamicMethodGenerator<Serializer.CreateGenericSurrogateDelegate>
    {
        public CreateGenericSurrogateMethodGenerator(Type originalType, Type surrogateType) : base(surrogateType)
        {
            ctorInfo = type.GetConstructor(new[] { originalType });
        }

        protected override MethodInfo GenerateInner()
        {
            var dynamicMethod = new DynamicMethod("CreateGenericSurrogate", returnType, parameterTypes, typeof(Serializer), true);
            var generator = dynamicMethod.GetILGenerator();

            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Newobj, ctorInfo);
            generator.Emit(OpCodes.Ret);

            return dynamicMethod;
        }

        private readonly ConstructorInfo ctorInfo;
    }
}

