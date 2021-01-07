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
    internal class SurrogateObjectIfNeededMethodGenerator : DynamicWriteMethodGenerator<SurrogateObjectIfNeededDelegate>
    {
        public SurrogateObjectIfNeededMethodGenerator(Type type, SwapList surrogatesForObjects, bool disableStamping, bool treatCollectionAsUserObject) 
            : base(type, "SurrogateObjectIfNeeded", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_0)
        {
            this.surrogatesForObjects = surrogatesForObjects;
        }

        protected override bool InnerGenerate(WriterGenerationContext context)
        {
            var surrogateId = surrogatesForObjects.FindMatchingIndex(type);
            if(surrogateId == -1)
            {
                return false;
            }

            var objVariable = new Variable(1);

            context.PushObjectWriterOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectWriter, SwapList>(x => x.surrogatesForObjects);
            context.Generator.PushIntegerOntoStack(surrogateId);
            context.Generator.Call<SwapList>(x => x.GetByIndex(0));

            // call surrogate factory to obtain surrogate object
            var delegateType = typeof(Func<,>).MakeGenericType(type, typeof(object));
            context.Generator.Emit(OpCodes.Castclass, delegateType);
            context.Generator.PushVariableOntoStack(objVariable);
            if(type.IsValueType)
            {
                context.Generator.Emit(OpCodes.Unbox_Any, type);
            }
            context.Generator.Emit(OpCodes.Call, delegateType.GetMethod("Invoke"));
            context.Generator.StoreVariableValueFromStack(objVariable);

            context.PushObjectWriterOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectWriter, ObjectIdentifier>(x => x.identifier);
            context.Generator.PushVariableOntoStack(objVariable); // object reference
            context.Generator.Emit(OpCodes.Ldarg_2); // reference id
            context.Generator.Call<ObjectIdentifier>(x => x.SetIdentifierForObject(null, 0));

            context.Generator.PushVariableOntoStack(objVariable);

            return true;
        }

        private readonly SwapList surrogatesForObjects;
    }
}

