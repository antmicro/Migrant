//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace Antmicro.Migrant.Generators
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

