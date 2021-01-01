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

