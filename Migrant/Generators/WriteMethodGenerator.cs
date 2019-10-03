/*
  Copyright (c) 2012 - 2016 Antmicro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)
   * Mateusz Holenko (mholenko@antmicro.com)

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Antmicro.Migrant.Hooks;
using System.Linq;
using Antmicro.Migrant.VersionTolerance;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.Generators
{
    internal class WriteMethodGenerator : DynamicMethodGenerator<WriteMethodDelegate>
    {
        internal WriteMethodGenerator(Type typeToGenerate, bool disableStamping, bool treatCollectionAsUserObject)
            : base(typeToGenerate, disableStamping, treatCollectionAsUserObject)
        {
            ObjectWriter.CheckLegality(typeToGenerate);
        }

        protected override MethodInfo GenerateInner()
        {
            DynamicMethod dynamicMethod = null;
            if(!type.IsArray)
            {
                dynamicMethod = new DynamicMethod(string.Format("Write_{0}", type.Name), returnType, parameterTypes, type, true);
            }
            else
            {
                var methodNo = Interlocked.Increment(ref WriteArrayMethodCounter);
                dynamicMethod = new DynamicMethod(string.Format("WriteArray{0}_{1}", methodNo, type.Name), returnType, parameterTypes, true);
            }
            var generator = dynamicMethod.GetILGenerator();
            var context = new WriterGenerationContext(generator, false, treatCollectionAsUserObject, OpCodes.Ldarg_0);

#if DEBUG_FORMAT
            GeneratorHelper.DumpToLibrary<WriteMethodDelegate>(context, c => GenerateDynamicCode((WriterGenerationContext)c, type), type.Name);
#endif
            GenerateDynamicCode(context, type);

            return dynamicMethod;
        }

        private void GenerateDynamicCode(WriterGenerationContext context, Type typeToGenerate)
        {
            var objectToSerialize = new Variable(1, typeToGenerate);

            // preserialization callbacks
            var exceptionBlockNeeded = Helpers.GetMethodsWithAttribute(typeof(PostSerializationAttribute), typeToGenerate).Any() ||
                                              Helpers.GetMethodsWithAttribute(typeof(LatePostSerializationAttribute), typeToGenerate).Any();
            if(exceptionBlockNeeded)
            {
                context.Generator.BeginExceptionBlock();
            }

            GenerateInvokeCallback(context, objectToSerialize, typeToGenerate, typeof(PreSerializationAttribute));

            if(!GenerateSpecialWrite(context, typeToGenerate, objectToSerialize, !treatCollectionAsUserObject))
            {
                GenerateWriteFields(context, objectToSerialize, typeToGenerate);
            }

            if(exceptionBlockNeeded)
            {
                context.Generator.BeginFinallyBlock();
            }

            if(exceptionBlockNeeded)
            {
                context.Generator.EndExceptionBlock();
            }
            context.Generator.Emit(OpCodes.Ret);
        }

        private void GenerateInvokeCallback(WriterGenerationContext context, Variable value, Type actualType, Type attributeType)
        {
            var methodsWithAttribute = Helpers.GetMethodsWithAttribute(attributeType, actualType);
            foreach(var method in methodsWithAttribute)
            {
                if(!method.IsStatic)
                {
                    context.Generator.PushVariableOntoStack(value);
                }

                context.Generator.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            }
        }

        private void GenerateAddCallbackToInvokeList(WriterGenerationContext context, LocalBuilder valueLocal, Type actualType, Type attributeType)
        {
            var actionCtor = typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
            var addToListMethod = Helpers.GetMethodInfo<List<Action>>(x => x.Add(null));

            var methodsWithAttribute = Helpers.GetMethodsWithAttribute(attributeType, actualType).ToList();
            var count = methodsWithAttribute.Count;
            if(count > 0)
            {
                context.PushObjectWriterOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectWriter, List<Action>>(x => x.postSerializationHooks);
            }
            for(var i = 1; i < count; i++)
            {
                context.Generator.Emit(OpCodes.Dup);
            }
            foreach(var method in methodsWithAttribute)
            {
                // let's make the delegate
                if(method.IsStatic)
                {
                    context.Generator.Emit(OpCodes.Ldnull);
                }
                else
                {
                    context.Generator.PushLocalValueOntoStack(valueLocal);
                }
                context.Generator.Emit(OpCodes.Ldftn, method);
                context.Generator.Emit(OpCodes.Newobj, actionCtor);
                // and add it to invoke list
                context.Generator.Emit(OpCodes.Call, addToListMethod);
            }
        }

        private static void GenerateWriteFields(WriterGenerationContext context, Variable value, Type actualType)
        {
            var fields = StampHelpers.GetFieldsInSerializationOrder(actualType);
            foreach(var field in fields)
            {
                var fieldValueLocal = context.Generator.DeclareLocal(field.FieldType);
                var fieldValue = new Variable(fieldValueLocal);
                
                context.Generator.PushVariableOntoStack(value);
                if(!actualType.IsValueType)
                {
                    context.Generator.Emit(OpCodes.Castclass, actualType);
                }
                context.Generator.Emit(OpCodes.Ldfld, field);
                context.Generator.StoreLocalValueFromStack(fieldValueLocal);

                GenerateWriteField(context, fieldValue, field.FieldType);
            }
        }

        internal static bool GenerateSpecialWrite(WriterGenerationContext context, Type actualType, Variable value, bool checkForCollections)
        {
            if(actualType.IsValueType)
            {
                // value type encountered here means it is in fact boxed value type
                // according to protocol it is written as it would be written inlined
                GenerateWriteValue(context, value, actualType);
                return true;
            }
            if(actualType.IsArray)
            {
                GenerateWriteArray(context, value, actualType);
                return true;
            }
            if(typeof(MulticastDelegate).IsAssignableFrom(actualType))
            {
                GenerateWriteDelegate(context, value);
                return true;
            }
            if(checkForCollections)
            {
                CollectionMetaToken collectionToken;
                if(CollectionMetaToken.TryGetCollectionMetaToken(actualType, out collectionToken))
                {
                    GenerateWriteEnumerable(context, value, collectionToken);
                    return true;
                }
            }
            return false;
        }

        private static void GenerateWriteArray(WriterGenerationContext context, Variable arrayLocal, Type actualType)
        {
            var rank = actualType.GetArrayRank();
            if(rank != 1)
            {
                GenerateWriteMultidimensionalArray(context, arrayLocal, actualType, rank);
                return;
            }

            var elementType = actualType.GetElementType();
            var currentElementLocal = context.Generator.DeclareLocal(elementType);
            var lengthLocal = context.Generator.DeclareLocal(typeof(int));
            var currentElementVariable = new Variable(currentElementLocal);

            context.Generator.PushVariableOntoStack(arrayLocal);
            context.Generator.Emit(OpCodes.Ldlen);
            context.Generator.StoreLocalValueFromStack(lengthLocal);

            GeneratorHelper.GenerateLoop(context, lengthLocal, c =>
            {
                context.Generator.PushVariableOntoStack(arrayLocal);
                context.Generator.PushLocalValueOntoStack(c);

                context.Generator.Emit(OpCodes.Ldelem, elementType);
                context.Generator.StoreLocalValueFromStack(currentElementLocal);

                GenerateWriteField(context, currentElementVariable, elementType);
            });
        }

        private static void GenerateWriteMultidimensionalArray(WriterGenerationContext context, Variable arrayLocal, Type actualType, int rank)
        {
            var elementType = actualType.GetElementType();

            var indexLocals = new LocalBuilder[rank];
            var lengthLocals = new LocalBuilder[rank];

            for(var i = 0; i < rank; i++)
            {
                indexLocals[i] = context.Generator.DeclareLocal(typeof(int));
                lengthLocals[i] = context.Generator.DeclareLocal(typeof(int));

                context.Generator.PushIntegerOntoStack(0);
                context.Generator.StoreLocalValueFromStack(indexLocals[i]);

                context.Generator.PushVariableOntoStack(arrayLocal);
                context.Generator.PushIntegerOntoStack(i);
                context.Generator.Call<Array>(x => x.GetLength(0));
                context.Generator.StoreLocalValueFromStack(lengthLocals[i]);
            }

            // writing elements
            var currentElementLocal = context.Generator.DeclareLocal(elementType);
            var currentElementVariable = new Variable(currentElementLocal);
            GenerateArrayWriteLoop(context, 0, rank, indexLocals, lengthLocals, arrayLocal, currentElementVariable, actualType, elementType);
        }

        private static void GenerateArrayWriteLoop(WriterGenerationContext context, int currentDimension, int rank, LocalBuilder[] indexLocals, LocalBuilder[] lengthLocals, Variable arrayLocal, Variable currentElementVariable, Type arrayType, Type elementType)
        {
            GeneratorHelper.GenerateLoop(context, lengthLocals[currentDimension], indexLocals[currentDimension], () =>
            {
                if(currentDimension == rank - 1)
                {
                    context.Generator.PushVariableOntoStack(arrayLocal);
                    for(var i = 0; i < rank; i++)
                    {
                        context.Generator.PushLocalValueOntoStack(indexLocals[i]);
                    }
                    // jeśli to nie zadziała to użyć:
                    context.Generator.Emit(OpCodes.Call, arrayType.GetMethod("Get"));
                    context.Generator.StoreVariableValueFromStack(currentElementVariable);
                    GenerateWriteField(context, currentElementVariable, elementType);
                }
                else
                {
                    GenerateArrayWriteLoop(context, currentDimension + 1, rank, indexLocals, lengthLocals, arrayLocal, currentElementVariable, arrayType, elementType);
                }
            });
        }

        private static void GenerateWriteEnumerable(WriterGenerationContext context, Variable valueLocal, CollectionMetaToken token)
        {
            var genericTypes = new[] { token.FormalElementType };
            var enumerableType = token.IsGeneric ? typeof(IEnumerable<>).MakeGenericType(genericTypes) : typeof(IEnumerable);
            var enumeratorType = token.IsGeneric ? typeof(IEnumerator<>).MakeGenericType(genericTypes) : typeof(IEnumerator);

            var iteratorLocal = context.Generator.DeclareLocal(enumeratorType);
            var currentElementLocal = context.Generator.DeclareLocal(token.FormalElementType);
            var elementVariable = new Variable(currentElementLocal);

            var loopBegin = context.Generator.DefineLabel();
            var finish = context.Generator.DefineLabel();

            context.PushPrimitiveWriterOntoStack();
            context.Generator.PushVariableOntoStack(valueLocal);
            context.Generator.Emit(token.CountMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, token.CountMethod);
            context.Generator.Call<PrimitiveWriter>(x => x.Write(0));

            var getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
            context.Generator.PushVariableOntoStack(valueLocal);
            context.Generator.Emit(OpCodes.Callvirt, getEnumeratorMethod);
            context.Generator.StoreLocalValueFromStack(iteratorLocal);

            context.Generator.MarkLabel(loopBegin);
            context.Generator.PushLocalValueOntoStack(iteratorLocal);
            context.Generator.Callvirt<IEnumerator>(x => x.MoveNext());
            context.Generator.Emit(OpCodes.Brfalse, finish);

            context.Generator.PushLocalValueOntoStack(iteratorLocal);
            context.Generator.Emit(OpCodes.Callvirt, enumeratorType.GetProperty("Current").GetGetMethod());
            context.Generator.StoreLocalValueFromStack(currentElementLocal);

            GenerateWriteField(context, elementVariable, token.FormalElementType);
            context.Generator.Emit(OpCodes.Br, loopBegin);

            context.Generator.MarkLabel(finish);
        }

        private static void GenerateWriteField(WriterGenerationContext context, Variable valueLocal, Type formalType)
        {
            switch(Helpers.GetSerializationType(formalType))
            {
            case SerializationType.Transient:
                // just omit it
                return;
            case SerializationType.Value:
                GenerateWriteValue(context, valueLocal, formalType);
                break;
            case SerializationType.Reference:
                GenerateWriteDeferredReference(context, valueLocal, formalType);
                break;
            }
        }

        private static void GenerateWriteDelegate(WriterGenerationContext context, Variable valueLocal)
        {
            var array = context.Generator.DeclareLocal(typeof(Delegate[]));
            var loopLength = context.Generator.DeclareLocal(typeof(int));
            var element = context.Generator.DeclareLocal(typeof(Delegate));
            var delegateTargetLocal = context.Generator.DeclareLocal(typeof(object));
            var delegateTargetVariable = new Variable(delegateTargetLocal);

            context.PushPrimitiveWriterOntoStack();

            context.PushObjectWriterOntoStack();
            context.Generator.PushVariableOntoStack(valueLocal);
            context.Generator.Call<ObjectWriter>(x => x.GetDelegatesWithNonTransientTargets(null));
            context.Generator.Emit(OpCodes.Castclass, typeof(Delegate[]));
            context.Generator.Emit(OpCodes.Dup);
            context.Generator.StoreLocalValueFromStack(array);

            // array refrence should be on the top of stack here
            context.Generator.Emit(OpCodes.Ldlen);
            context.Generator.Emit(OpCodes.Dup);
            context.Generator.StoreLocalValueFromStack(loopLength);

            // primitive writer should be on the stack
            // array length should be on the stack
            context.Generator.Call<PrimitiveWriter>(x => x.Write(0));

            GeneratorHelper.GenerateLoop(context, loopLength, c =>
            {
                context.Generator.PushLocalValueOntoStack(array);
                context.Generator.PushLocalValueOntoStack(c);
                context.Generator.Emit(OpCodes.Ldelem, element.LocalType);
                context.Generator.Emit(OpCodes.Dup);
                context.Generator.StoreLocalValueFromStack(element);

                // element reference should be on the stack
                context.Generator.PushPropertyValueOntoStack<MulticastDelegate, object>(x => x.Target);
                context.Generator.StoreLocalValueFromStack(delegateTargetLocal);

                GenerateWriteDeferredReference(context, delegateTargetVariable, typeof(object));

                context.PushObjectWriterOntoStack();
                context.Generator.PushPropertyValueOntoStack<ObjectWriter, IdentifiedElementsDictionary<MethodDescriptor>>(x => x.Methods);
                context.Generator.PushLocalValueOntoStack(element);
                context.Generator.PushPropertyValueOntoStack<MulticastDelegate, MethodInfo>(x => x.Method);
                context.Generator.Emit(OpCodes.Newobj, Helpers.GetConstructorInfo<MethodDescriptor>(typeof(MethodInfo)));
                context.Generator.Call<IdentifiedElementsDictionary<MethodDescriptor>>(x => x.TouchAndWriteId(null));
                context.Generator.Emit(OpCodes.Pop);
            });
        }

        private static void GenerateWriteValue(WriterGenerationContext context, Variable valueLocal, Type formalType)
        {
            ObjectWriter.CheckLegality(formalType);
            if(formalType.IsEnum)
            {
                formalType = Enum.GetUnderlyingType(formalType);
            }
            var writeMethod = typeof(PrimitiveWriter).GetMethod("Write", new[] { formalType });
            // if this method is null, then it is a non-primitive (i.e. custom) struct
            if(writeMethod != null)
            {
                context.PushPrimitiveWriterOntoStack();
                context.Generator.PushVariableOntoStack(valueLocal);
                context.Generator.Emit(OpCodes.Call, writeMethod);
                return;
            }
            var nullableUnderlyingType = Nullable.GetUnderlyingType(formalType);
            if(nullableUnderlyingType != null)
            {
                var hasValueLabel = context.Generator.DefineLabel();
                var finishLabel = context.Generator.DefineLabel();

                var underlyingValueLocal = context.Generator.DeclareLocal(nullableUnderlyingType);
                var underlyingVariable = new Variable(underlyingValueLocal);
                
                context.PushPrimitiveWriterOntoStack();
                context.Generator.PushVariableAddressOntoStack(valueLocal);
                context.Generator.Emit(OpCodes.Call, formalType.GetProperty("HasValue").GetGetMethod());
                context.Generator.Emit(OpCodes.Brtrue_S, hasValueLabel);
                context.Generator.PushIntegerOntoStack(0);
                context.Generator.Call<PrimitiveWriter>(x => x.Write(false));
                context.Generator.Emit(OpCodes.Br_S, finishLabel);

                context.Generator.MarkLabel(hasValueLabel);
                context.Generator.PushIntegerOntoStack(1);
                context.Generator.Call<PrimitiveWriter>(x => x.Write(false));

                context.Generator.PushVariableAddressOntoStack(valueLocal);
                context.Generator.Emit(OpCodes.Call, formalType.GetProperty("Value").GetGetMethod());
                context.Generator.StoreLocalValueFromStack(underlyingValueLocal);

                GenerateWriteValue(context, underlyingVariable, nullableUnderlyingType);

                context.Generator.MarkLabel(finishLabel);
                return;
            }

            GenerateWriteFields(context, valueLocal, formalType);
        }

        internal static void GenerateWriteDeferredReference(WriterGenerationContext context, Variable valueLocal, Type formalType)
        {
            var finish = context.Generator.DefineLabel();
            var isNotNull = context.Generator.DefineLabel();
            var isNotTransient = context.Generator.DefineLabel();

            context.Generator.PushVariableOntoStack(valueLocal);
            context.Generator.Emit(OpCodes.Brtrue_S, isNotNull);

            context.PushNullReferenceOnStack();
            context.Generator.Emit(OpCodes.Br, finish);

            context.Generator.MarkLabel(isNotNull);
            var formalTypeIsActualType = (formalType.Attributes & TypeAttributes.Sealed) != 0;
            if(!formalTypeIsActualType)
            {
                context.Generator.PushVariableOntoStack(valueLocal);
                context.Generator.Call(() => Helpers.IsTransient((object)null));

                context.Generator.Emit(OpCodes.Brfalse_S, isNotTransient);
                context.PushNullReferenceOnStack();
                context.Generator.Emit(OpCodes.Br, finish);

                context.Generator.MarkLabel(isNotTransient);
                context.PushObjectWriterOntoStack();
                context.Generator.PushVariableOntoStack(valueLocal);
                context.Generator.Call<ObjectWriter>(x => x.WriteDeferredReference(null));
            }
            else
            {
                if(Helpers.IsTransient(formalType))
                {
                    context.PushNullReferenceOnStack();
                }
                else
                {
                    context.PushObjectWriterOntoStack();
                    context.Generator.PushVariableOntoStack(valueLocal);
                    context.Generator.Call<ObjectWriter>(x => x.WriteDeferredReference(null));
                }
            }
            context.Generator.MarkLabel(finish);
        }

        internal static bool GenerateTryWriteObjectInline(WriterGenerationContext context, bool generatePreSerializationCallback, bool generatePostSerializationCallback, Variable valueLocal, Type actualType)
        {
            if(actualType.IsArray)
            {
                var rank = actualType.GetArrayRank();

                // write rank
                context.PushPrimitiveWriterOntoStack();
                context.Generator.PushIntegerOntoStack(rank);
                context.Generator.Call<PrimitiveWriter>(x => x.Write(0));

                if(rank == 1)
                {
                    // write length
                    context.PushPrimitiveWriterOntoStack();
                    context.Generator.PushVariableOntoStack(valueLocal);
                    context.Generator.Emit(OpCodes.Castclass, actualType);
                    context.Generator.Emit(OpCodes.Ldlen);
                    context.Generator.Call<PrimitiveWriter>(x => x.Write(0));
                }
                else
                {
                    // write lengths in loop
                    for(var i = 0; i < rank; i++)
                    {
                        context.PushPrimitiveWriterOntoStack();
                        context.Generator.PushVariableOntoStack(valueLocal);
                        context.Generator.PushIntegerOntoStack(i);
                        context.Generator.Call<Array>(x => x.GetLength(0));
                        context.Generator.Call<PrimitiveWriter>(x => x.Write(0));
                    }
                }
                return false;
            }
            if(actualType == typeof(string))
            {
                GenerateInvokeCallbacksAndExecute(context, generatePreSerializationCallback, generatePostSerializationCallback, valueLocal, actualType, c =>
                {
                    c.PushPrimitiveWriterOntoStack();
                    c.Generator.PushVariableOntoStack(valueLocal);
                    c.Generator.Call<PrimitiveWriter>(x => x.Write((string)null));
                });

                return true;
            }

            return GenerateSpecialWrite(context, actualType, valueLocal, false);
        }

        private static void GenerateInvokeCallbacksAndExecute(WriterGenerationContext context, bool generatePreSerializationCallback, bool generatePostSerializationCallback, Variable valueLocal, Type type, Action<WriterGenerationContext> bodyBuilder)
        {
            if(generatePreSerializationCallback || generatePostSerializationCallback)
            {
                context.Generator.BeginExceptionBlock();
            }

            if(generatePreSerializationCallback)
            {
                context.PushObjectWriterOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectWriter, Action<object>>(x => x.preSerializationCallback);
                context.Generator.PushVariableOntoStack(valueLocal);
                context.Generator.Call<Action<object>>(x => x.Invoke(null));
            }

            bodyBuilder(context);

            if(generatePreSerializationCallback || generatePostSerializationCallback)
            {
                context.Generator.BeginFinallyBlock();
            }

            if(generatePostSerializationCallback)
            {
                context.PushObjectWriterOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectWriter, Action<object>>(x => x.postSerializationCallback);
                context.Generator.PushVariableOntoStack(valueLocal);
                context.Generator.Call<Action<object>>(x => x.Invoke(null));
            }

            if(generatePreSerializationCallback || generatePostSerializationCallback)
            {
                context.Generator.EndExceptionBlock();
            }
        }

        private static int WriteArrayMethodCounter;
    }
}

