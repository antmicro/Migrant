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
    internal class TouchInlinedObjectGenerator : DynamicReadMethodGenerator<TouchInlinedObjectMethodDelegate>
    {
        public TouchInlinedObjectGenerator(Type type, bool disableStamping, bool treatCollectionAsUserObject)
            : base(type, "TouchInlinedObject", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_1, OpCodes.Ldarg_0)
        {
        }

        protected override void InnerGenerate(ReaderGenerationContext context)
        {
            var finish = context.Generator.DefineLabel();
            var createdLocal = context.Generator.DeclareLocal(typeof(object));

            context.PushObjectIdOntoStack();
            context.PushDeserializedObjectsCollectionOntoStack();
            context.Generator.PushPropertyValueOntoStack<AutoResizingList<object>, int>(x => x.Count);
            context.Generator.Emit(OpCodes.Blt, finish);

            if(CreateObjectGenerator.TryFillBody(context.Generator, type, treatCollectionAsUserObject))
            {
                context.Generator.StoreLocalValueFromStack(createdLocal);
                context.PushObjectReaderOntoStack();
                context.PushObjectIdOntoStack();
                context.Generator.PushLocalValueOntoStack(createdLocal);
                context.Generator.Call<ObjectReader>(x => x.SetObjectByReferenceId(0, null));
            }
            else if(type.IsArray)
            {
                var isMultidimensional = type.GetArrayRank() > 1;
                var elementFormalType = type.GetElementType();

                var rankLocal = context.Generator.DeclareLocal(typeof(int));
                var lengthsLocal = isMultidimensional ? context.Generator.DeclareLocal(typeof(int[])) : context.Generator.DeclareLocal(typeof(int));

                context.PushObjectReaderOntoStack();
                context.PushObjectIdOntoStack();

                ReadMethodGenerator.GenerateReadPrimitive(context, typeof(int));
                context.Generator.StoreLocalValueFromStack(rankLocal);
                if(isMultidimensional)
                {
                    context.Generator.PushLocalValueOntoStack(rankLocal);
                    context.Generator.Emit(OpCodes.Newarr, typeof(int));
                    context.Generator.StoreLocalValueFromStack(lengthsLocal); // create an array for keeping the lengths of each dimension

                    GeneratorHelper.GenerateLoop(context, rankLocal, i =>
                    {
                        context.Generator.PushLocalValueOntoStack(lengthsLocal);
                        context.Generator.PushLocalValueOntoStack(i);
                        ReadMethodGenerator.GenerateReadPrimitive(context, typeof(int));
                        context.Generator.Emit(OpCodes.Stelem, typeof(int)); // populate the lengths with values read from stream
                    });
                }
                else
                {
                    ReadMethodGenerator.GenerateReadPrimitive(context, typeof(int));
                    context.Generator.StoreLocalValueFromStack(lengthsLocal);
                }

                context.Generator.PushTypeOntoStack(elementFormalType);
                context.Generator.PushLocalValueOntoStack(lengthsLocal);

                if(isMultidimensional)
                {
                    context.Generator.Call(() => Array.CreateInstance(null, new int[0]));
                }
                else
                {
                    context.Generator.Call(() => Array.CreateInstance(null, 0));
                }

                context.Generator.Call<ObjectReader>(x => x.SetObjectByReferenceId(0, null));
            }
            else if(type == typeof(string))
            {
                context.PushObjectReaderOntoStack();
                context.PushObjectIdOntoStack();
                ReadMethodGenerator.GenerateReadPrimitive(context, typeof(string));
                context.Generator.Call<ObjectReader>(x => x.SetObjectByReferenceId(0, null));

                var field = Helpers.GetFieldInfo<ObjectReader, int>(x => x.objectsWrittenInlineCount);

                context.PushObjectReaderOntoStack();
                context.Generator.Emit(OpCodes.Dup);
                context.Generator.Emit(OpCodes.Ldfld, field);
                context.Generator.PushIntegerOntoStack(1);
                context.Generator.Emit(OpCodes.Add);
                context.Generator.Emit(OpCodes.Stfld, field);
            }
            else
            {
                ReadMethodGenerator.GenerateReadNotPrecreated(context, type, context.PushObjectIdOntoStack);

                var field = Helpers.GetFieldInfo<ObjectReader, int>(x => x.objectsWrittenInlineCount);

                context.PushObjectReaderOntoStack();
                context.Generator.Emit(OpCodes.Dup);
                context.Generator.Emit(OpCodes.Ldfld, field);
                context.Generator.PushIntegerOntoStack(1);
                context.Generator.Emit(OpCodes.Add);
                context.Generator.Emit(OpCodes.Stfld, field);
            }

            context.Generator.MarkLabel(finish);
        }
    }
}

