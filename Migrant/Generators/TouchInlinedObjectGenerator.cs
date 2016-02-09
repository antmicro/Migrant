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
using System.Collections.Generic;
using System.Reflection;
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

                context.PushObjectReaderOntoStack();
                context.Generator.Emit(OpCodes.Ldfld, typeof(ObjectReader).GetField("objectsWrittenInline", BindingFlags.Instance | BindingFlags.NonPublic));
                context.PushObjectIdOntoStack();
                context.Generator.Call<HashSet<int>>(x => x.Add(0));
                context.Generator.Emit(OpCodes.Pop);
            }
            else
            {
                ReadMethodGenerator.GenerateReadNotPrecreated(context, type, context.PushObjectIdOntoStack);

                context.PushObjectReaderOntoStack();
                context.Generator.Emit(OpCodes.Ldfld, typeof(ObjectReader).GetField("objectsWrittenInline", BindingFlags.Instance | BindingFlags.NonPublic));
                context.PushObjectIdOntoStack();
                context.Generator.Call<HashSet<int>>(x => x.Add(0));
                context.Generator.Emit(OpCodes.Pop);
            }

            context.Generator.MarkLabel(finish);
        }
    }
}

