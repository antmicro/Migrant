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
using System.Runtime.Serialization;

namespace Migrantoid.Generators
{
    internal class CreateObjectGenerator : DynamicMethodGenerator<CreateObjectMethodDelegate>
    {
        public CreateObjectGenerator(Type type, bool disableStamping, bool treatCollectionAsUserObject)
            : base(type, disableStamping, treatCollectionAsUserObject)
        {
        }

        protected override MethodInfo GenerateInner()
        {
            var dynamicMethod = new DynamicMethod("CreateInstance", returnType, parameterTypes, type, true);
            var generator = dynamicMethod.GetILGenerator();
            FillBody(generator);
            generator.Emit(OpCodes.Ret);
            return dynamicMethod;
        }

        private void FillBody(ILGenerator generator)
        {
            if(!TryFillBody(generator, type, treatCollectionAsUserObject))
            {
                generator.Emit(OpCodes.Ldnull);
            }
        }

        public static bool TryFillBody(ILGenerator generator, Type type, bool treatCollectionAsUserObject, Action<ILGenerator> beforeCreationAction = null)
        {
            var creationWay = ObjectReader.GetCreationWay(type, treatCollectionAsUserObject);
            if(creationWay == ObjectReader.CreationWay.Null)
            {
                return false;
            }

            if(beforeCreationAction != null)
            {
                beforeCreationAction(generator);
            }

            generator.PushTypeOntoStack(type);
            switch(creationWay)
            {
                case ObjectReader.CreationWay.DefaultCtor:
                generator.PushIntegerOntoStack(1);
                generator.Call(() => Activator.CreateInstance(type, default(bool)));
                break;
                case ObjectReader.CreationWay.Uninitialized:
                generator.Call(() => FormatterServices.GetUninitializedObject(type));
                break;
            }

            return true;
        }
    }
}

