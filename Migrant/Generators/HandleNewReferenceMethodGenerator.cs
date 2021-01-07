//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.Generators
{
    internal class HandleNewReferenceMethodGenerator : DynamicWriteMethodGenerator<HandleNewReferenceMethodDelegate>
    {
        public HandleNewReferenceMethodGenerator(Type type, SwapList objectsForSurrogates, bool disableStamping, bool treatCollectionAsUserObject, bool generatePreSerializationCallback, bool generatePostSerializationCallback)
            : base(type, "HandleNewReference", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_0)
        {
            this.objectsForSurrogates = objectsForSurrogates;
            this.generatePreSerializationCallback = generatePreSerializationCallback;
            this.generatePostSerializationCallback = generatePostSerializationCallback;
        }

        protected override bool InnerGenerate(WriterGenerationContext context)
        {
            Variable value;
            if(type.IsValueType)
            {            
                var valueLocal = context.Generator.DeclareLocal(type);
                context.Generator.Emit(OpCodes.Ldarg_1);
                context.Generator.Emit(OpCodes.Unbox_Any, type);
                context.Generator.StoreLocalValueFromStack(valueLocal);

                value = new Variable(valueLocal);
            }
            else
            {
                value = new Variable(1);
            }

            var objectForSurrogatesIndex = objectsForSurrogates == null ? -1 : objectsForSurrogates.FindMatchingIndex(type);
            context.PushPrimitiveWriterOntoStack();
            context.Generator.PushIntegerOntoStack(objectForSurrogatesIndex != -1 ? 1 : 0);
            context.Generator.Call<PrimitiveWriter>(x => x.Write(false));

            if(objectForSurrogatesIndex != -1)
            {
                context.PushObjectWriterOntoStack();
                context.Generator.Emit(OpCodes.Dup);
                context.Generator.PushFieldValueOntoStack<ObjectWriter, SwapList>(x => x.objectsForSurrogates);
                context.Generator.PushIntegerOntoStack(objectForSurrogatesIndex);
                context.Generator.Call<SwapList>(x => x.GetByIndex(0));

                var delegateType = typeof(Func<,>).MakeGenericType(type, typeof(object));
                context.Generator.Emit(OpCodes.Castclass, delegateType);
                context.Generator.Emit(OpCodes.Ldarg_1);
                context.Generator.Emit(OpCodes.Call, delegateType.GetMethod("Invoke"));

                // here should be a primitive writer on a stack
                context.Generator.Call<object>(x => x.GetType());
                context.Generator.Call<ObjectWriter>(x => x.TouchAndWriteTypeId(typeof(void)));
                context.Generator.Emit(OpCodes.Pop);
            }

            if(WriteMethodGenerator.GenerateTryWriteObjectInline(context, generatePreSerializationCallback, generatePostSerializationCallback, value, type))
            {
                context.PushObjectWriterOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectWriter, HashSet<int>>(x => x.objectsWrittenInline);
                context.Generator.Emit(OpCodes.Ldarg_2); // reference identifier
                context.Generator.Call<HashSet<int>>(x => x.Add(0));
                context.Generator.Emit(OpCodes.Pop);
            }

            return true;
        }

        private readonly bool generatePreSerializationCallback;
        private readonly bool generatePostSerializationCallback;
        private readonly SwapList objectsForSurrogates;
    }
}

