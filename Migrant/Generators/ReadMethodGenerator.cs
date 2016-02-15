/*
  Copyright (c) 2013-2016 Antmicro <www.antmicro.com>

  Authors:
   * Mateusz Holenko (mholenko@antmicro.com)
   * Konrad Kruczynski (kkruczynski@antmicro.com)

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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;
using System.Collections;
using Antmicro.Migrant.VersionTolerance;

namespace Antmicro.Migrant.Generators
{
    internal sealed class ReadMethodGenerator : DynamicMethodGenerator<ReadMethodDelegate>
    {
        public ReadMethodGenerator(Type typeToGenerate, bool disableStamping, bool treatCollectionAsUserObject)
            : base(typeToGenerate, disableStamping, treatCollectionAsUserObject)
        {
        }

        protected override MethodInfo GenerateInner()
        {
            DynamicMethod dynamicMethod;

            if(type.IsArray)
            {
                dynamicMethod = new DynamicMethod("Read", returnType, parameterTypes, true);
            }
            else
            {
                dynamicMethod = new DynamicMethod("Read", returnType, parameterTypes, type, true);
            }
            var generator = dynamicMethod.GetILGenerator();
            var context = new ReaderGenerationContext(generator, disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_2, OpCodes.Ldarg_0);
            GenerateDynamicCode(context, type);

#if DEBUG_FORMAT
            GeneratorHelper.DumpToLibrary<ReadMethodDelegate>(context, c => GenerateDynamicCode((ReaderGenerationContext)c, type));
#endif

            return dynamicMethod;
        }

        internal static void GenerateReadPrimitive(ReaderGenerationContext context, Type type)
        {
            context.PushPrimitiveReaderOntoStack();
            var mname = string.Concat("Read", type.Name);
            var readMethod = typeof(PrimitiveReader).GetMethod(mname);
            if(readMethod == null)
            {
                throw new ArgumentException("Method <<" + mname + ">> not found");
            }

            context.Generator.Emit(OpCodes.Call, readMethod);
        }

        internal static void GenerateReadNotPrecreated(ReaderGenerationContext context, Type formalType, Action pushObjectIdOntoStackAction)
        {
            if(formalType.IsValueType)
            {
                context.PushDeserializedObjectsCollectionOntoStack();
                pushObjectIdOntoStackAction();
                GenerateReadField(context, formalType, true);
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
            }
            else if(formalType.IsArray)
            {
                GenerateReadArray(context, formalType, pushObjectIdOntoStackAction);
            }
            else if(typeof(MulticastDelegate).IsAssignableFrom(formalType))
            {
                GenerateReadDelegate(context, formalType, pushObjectIdOntoStackAction);
            }
            else
            {
                throw new InvalidOperationException(InternalErrorMessage + "GenerateReadNotPrecreated");
            }
        }

        private static void GenerateDynamicCode(ReaderGenerationContext context, Type typeToGenerate)
        {
            GenerateReadObjectInner(context, typeToGenerate);
            context.Generator.Emit(OpCodes.Ret);
        }

        private static void GenerateReadObjectInner(ReaderGenerationContext context, Type formalType)
        {
            var finish = context.Generator.DefineLabel();
            var objectIdLocal = context.Generator.DeclareLocal(typeof(int));

            context.PushObjectIdOntoStack();
            context.Generator.StoreLocalValueFromStack(objectIdLocal);

            GenerateTouchObject(context, formalType);
            context.PushObjectReaderOntoStack();
            context.Generator.Call<ObjectReader>(x => x.ResetMaxAskedReferenceId());

            switch(ObjectReader.GetCreationWay(formalType, context.TreatCollectionAsUserObject))
            {
            case ObjectReader.CreationWay.Null:
                GenerateReadNotPrecreated(context, formalType, objectIdLocal);
                break;
            case ObjectReader.CreationWay.DefaultCtor:
                GenerateUpdateElements(context, formalType, objectIdLocal);
                break;
            case ObjectReader.CreationWay.Uninitialized:
                GenerateUpdateFields(context, formalType, objectIdLocal);
                break;
            }

            context.PushDeserializedObjectOntoStack(objectIdLocal);
            context.Generator.Emit(OpCodes.Brfalse, finish);

            context.Generator.MarkLabel(finish);
        }

        private static void GenerateUpdateElements(ReaderGenerationContext context, Type formalType, LocalBuilder objectIdLocal)
        {
            if(typeof(ISpeciallySerializable).IsAssignableFrom(formalType))
            {
                context.PushDeserializedObjectOntoStack(objectIdLocal);
                context.Generator.Emit(OpCodes.Castclass, typeof(ISpeciallySerializable));
                context.PushPrimitiveReaderOntoStack();
                context.Generator.GenerateCodeCall<ISpeciallySerializable, PrimitiveReader>(ObjectReader.LoadAndVerifySpeciallySerializableAndVerify);
                return;
            }

            CollectionMetaToken collectionToken;

            if(!CollectionMetaToken.TryGetCollectionMetaToken(formalType, out collectionToken))
            {
                throw new InvalidOperationException(InternalErrorMessage);
            }

            GenerateFillCollection(context, collectionToken.FormalElementType, formalType, objectIdLocal);
        }

        private static void GenerateFillCollection(ReaderGenerationContext context, Type elementFormalType, Type collectionType, LocalBuilder objectIdLocal)
        {
            var countLocal = context.Generator.DeclareLocal(typeof(int));

            GenerateReadPrimitive(context, typeof(int));
            context.Generator.StoreLocalValueFromStack(countLocal); // read collection elements count

            var addMethod = collectionType.GetMethod("Add", new[] { elementFormalType }) ??
                   collectionType.GetMethod("Enqueue", new[] { elementFormalType }) ??
                   collectionType.GetMethod("Push", new[] { elementFormalType });
            if(addMethod == null)
            {
                throw new InvalidOperationException(string.Format(CouldNotFindAddErrorMessage, collectionType));
            }

            if(collectionType == typeof(Stack) ||
            (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(Stack<>)))
            {
                var tempArrLocal = context.Generator.DeclareLocal(elementFormalType.MakeArrayType());

                context.Generator.PushLocalValueOntoStack(countLocal);
                context.Generator.Emit(OpCodes.Newarr, elementFormalType);
                context.Generator.StoreLocalValueFromStack(tempArrLocal); // creates temporal array

                GeneratorHelper.GenerateLoop(context, countLocal, cl =>
                {
                    context.Generator.PushLocalValueOntoStack(tempArrLocal);
                    context.Generator.PushLocalValueOntoStack(cl);
                    GenerateReadField(context, elementFormalType, false);
                    context.Generator.Emit(OpCodes.Stelem, elementFormalType);
                });

                GeneratorHelper.GenerateLoop(context, countLocal, cl =>
                {
                    context.PushDeserializedObjectOntoStack(objectIdLocal);
                    context.Generator.Emit(OpCodes.Castclass, collectionType);

                    context.Generator.PushLocalValueOntoStack(tempArrLocal);
                    context.Generator.PushLocalValueOntoStack(cl);
                    context.Generator.Emit(OpCodes.Ldelem, elementFormalType);
                    context.Generator.Emit(OpCodes.Callvirt, collectionType.GetMethod("Push"));
                }, true);
            }
            else
            {
                GeneratorHelper.GenerateLoop(context, countLocal, cl =>
                {
                    context.PushDeserializedObjectOntoStack(objectIdLocal);
                    context.Generator.Emit(OpCodes.Castclass, collectionType);
                    GenerateReadField(context, elementFormalType, false);
                    context.Generator.Emit(OpCodes.Callvirt, addMethod);

                    if(addMethod.ReturnType != typeof(void))
                    {
                        context.Generator.Emit(OpCodes.Pop); // remove returned unused value from stack
                    }
                });
            }
        }

        private static void GenerateUpdateFields(ReaderGenerationContext context, Type formalType, LocalBuilder objectIdLocal)
        {
            var fields = context.DisableStamping
                ? ((TypeSimpleDescriptor)formalType).FieldsToDeserialize
                : ((TypeFullDescriptor)formalType).FieldsToDeserialize;

            foreach(var fieldOrType in fields)
            {
                if(fieldOrType.Field == null)
                {
                    GenerateReadField(context, fieldOrType.TypeToOmit, false);
                    context.Generator.Emit(OpCodes.Pop);
                    continue;
                }
                var field = fieldOrType.Field;

                if(field.IsDefined(typeof(TransientAttribute), false))
                {
                    if(field.IsDefined(typeof(ConstructorAttribute), false))
                    {
                        context.Generator.PushFieldInfoOntoStack(field);
                        context.PushDeserializedObjectOntoStack(objectIdLocal);

                        context.Generator.GenerateCodeCall<FieldInfo, object>((fi, target) =>
                        {
                            // this code is done using reflection and not generated due to
                            // small estimated profit and lot of code to write:
                            // * copying constructor attributes from generating to generated code
                            // * calculating optimal constructor to call based on a collection of arguments
                            var ctorAttribute = (ConstructorAttribute)fi.GetCustomAttributes(false).First(x => x is ConstructorAttribute);
                            fi.SetValue(target, Activator.CreateInstance(fi.FieldType, ctorAttribute.Parameters));
                        });
                    }
                    continue;
                }

                context.PushDeserializedObjectOntoStack(objectIdLocal);
                GenerateReadField(context, field.FieldType, false);
                context.Generator.Emit(OpCodes.Stfld, field);
            }
        }

        private static void SaveNewDeserializedObject(ReaderGenerationContext context, LocalBuilder objectIdLocal, Action generateNewObject)
        {
            context.PushDeserializedObjectsCollectionOntoStack();
            context.Generator.PushLocalValueOntoStack(objectIdLocal);

            generateNewObject();

            context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
        }

        private static void GenerateTouchObject(ReaderGenerationContext context, Type formalType)
        {
            var finish = context.Generator.DefineLabel();

            context.PushObjectIdOntoStack();
            context.PushDeserializedObjectsCollectionOntoStack();
            context.Generator.PushPropertyValueOntoStack<AutoResizingList<object>, int>(x => x.Count);
            // if (refId < deserializedObjects.Count) return;
            context.Generator.Emit(OpCodes.Blt, finish);

            if(CreateObjectGenerator.TryFillBody(context.Generator, formalType, context.TreatCollectionAsUserObject, x =>
            {
                context.PushObjectReaderOntoStack();
                context.PushObjectIdOntoStack();
            }))
            {
                context.Generator.Call<ObjectReader>(x => x.SetObjectByReferenceId(0, null));
            }

            context.Generator.MarkLabel(finish);
        }

        private static void GenerateReadNotPrecreated(ReaderGenerationContext context, Type formalType, LocalBuilder objectIdLocal)
        {
            GenerateReadNotPrecreated(context, formalType, () => context.Generator.PushLocalValueOntoStack(objectIdLocal));
        }

        private static void GenerateReadField(ReaderGenerationContext context, Type formalType, bool boxIfValueType = true)
        {
            // method returns read field value on stack

            if(Helpers.IsTransient(formalType))
            {
                context.Generator.PushTypeOntoStack(formalType);
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, object>(x => Helpers.GetDefaultValue(null)));

                if(formalType.IsValueType && boxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Box, formalType);
                }
                return; //return Helpers.GetDefaultValue(_formalType);
            }

            var finishLabel = context.Generator.DefineLabel();

            if(!formalType.IsValueType)
            {
                var referenceIdLocal = context.Generator.DeclareLocal(typeof(int));
                var returnDeserializeObjectLabel = context.Generator.DefineLabel();

                context.PushObjectReaderOntoStack();
                context.Generator.Call<ObjectReader>(x => x.ReadAndTouchReference());

                context.Generator.Emit(OpCodes.Dup);
                context.Generator.StoreLocalValueFromStack(referenceIdLocal);
                context.Generator.Emit(OpCodes.Ldc_I4, Consts.NullObjectId);
                context.Generator.Emit(OpCodes.Bne_Un, returnDeserializeObjectLabel);

                context.Generator.Emit(OpCodes.Ldnull);
                context.Generator.Emit(OpCodes.Br, finishLabel);

                context.Generator.MarkLabel(returnDeserializeObjectLabel);

                context.PushDeserializedObjectOntoStack(referenceIdLocal, true);
                context.Generator.Emit(OpCodes.Castclass, formalType);

                context.Generator.MarkLabel(finishLabel);
                return;
            }

            var continueWithNullableLabel = context.Generator.DefineLabel();
            var forcedFormalType = formalType;
            var forcedBoxIfValueType = boxIfValueType;

            var nullableActualType = Nullable.GetUnderlyingType(formalType);
            if(nullableActualType != null)
            {
                forcedFormalType = nullableActualType;
                forcedBoxIfValueType = true;

                GenerateReadPrimitive(context, typeof(bool));
                context.Generator.Emit(OpCodes.Brtrue, continueWithNullableLabel);

                context.Generator.Emit(OpCodes.Ldnull);
                context.Generator.Emit(OpCodes.Br, finishLabel);

                context.Generator.MarkLabel(continueWithNullableLabel);
            }

            if(forcedFormalType.IsEnum)
            {
                var actualType = Enum.GetUnderlyingType(forcedFormalType);

                context.Generator.PushTypeOntoStack(forcedFormalType);
                GenerateReadPrimitive(context, actualType);
                context.Generator.Emit(OpCodes.Call, typeof(Enum).GetMethod("ToObject", BindingFlags.Static | BindingFlags.Public, null, new[] {
                    typeof(Type),
                    actualType
                }, null));

                if(!forcedBoxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Unbox_Any, forcedFormalType);
                }
            }
            else if(Helpers.IsWriteableByPrimitiveWriter(forcedFormalType))
            {
                // value type
                GenerateReadPrimitive(context, forcedFormalType);

                if(forcedBoxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Box, forcedFormalType);
                }
            }
            else
            {
                // here we have struct
                var structLocal = context.Generator.DeclareLocal(forcedFormalType);
                GenerateUpdateStructFields(context, forcedFormalType, structLocal);
                context.Generator.PushLocalValueOntoStack(structLocal);
                if(forcedBoxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Box, forcedFormalType);
                }
            }

            context.Generator.MarkLabel(finishLabel);

            // if the value is nullable we must use special initialization of it
            if(nullableActualType != null)
            {
                var nullableLocal = context.Generator.DeclareLocal(formalType);
                var returnNullNullableLabel = context.Generator.DefineLabel();
                var endLabel = context.Generator.DefineLabel();

                context.Generator.Emit(OpCodes.Dup);
                context.Generator.Emit(OpCodes.Brfalse, returnNullNullableLabel);

                if(forcedBoxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Unbox_Any, nullableActualType);
                }
                context.Generator.Emit(OpCodes.Newobj, formalType.GetConstructor(new[] { nullableActualType }));
                context.Generator.StoreLocalValueFromStack(nullableLocal);
                context.Generator.PushLocalValueOntoStack(nullableLocal);

                if(boxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Box, formalType);
                }

                context.Generator.Emit(OpCodes.Br, endLabel);

                context.Generator.MarkLabel(returnNullNullableLabel);
                context.Generator.Emit(OpCodes.Pop);
                context.Generator.PushLocalAddressOntoStack(nullableLocal);
                context.Generator.Emit(OpCodes.Initobj, formalType);

                context.Generator.PushLocalValueOntoStack(nullableLocal);
                if(boxIfValueType)
                {
                    context.Generator.Emit(OpCodes.Box, nullableLocal);
                }

                context.Generator.MarkLabel(endLabel);
            }
        }

        private static void GenerateUpdateStructFields(ReaderGenerationContext context, Type formalType, LocalBuilder structLocal)
        {
            var fields = context.DisableStamping
                                ? ((TypeSimpleDescriptor)formalType).FieldsToDeserialize
                                : ((TypeFullDescriptor)formalType).FieldsToDeserialize;

            foreach(var field in fields)
            {
                if(field.Field == null)
                {
                    GenerateReadField(context, field.TypeToOmit, false);
                    context.Generator.Emit(OpCodes.Pop);
                    continue;
                }

                if(field.Field.IsDefined(typeof(TransientAttribute), false))
                {
                    if(field.Field.IsDefined(typeof(ConstructorAttribute), false))
                    {
                        context.Generator.PushLocalAddressOntoStack(structLocal);
                        context.Generator.PushFieldInfoOntoStack(field.Field);
                        context.Generator.GenerateCodeFCall<FieldInfo, object>(fi =>
                        {
                            // this code is done using reflection and not generated due to
                            // small estimated profit and lot of code to write:
                            // * copying constructor attributes from generating to generated code
                            // * calculating optimal constructor to call based on a collection of arguments
                            var ctorAttribute = (ConstructorAttribute)fi.GetCustomAttributes(false).First(x => x is ConstructorAttribute);
                            return Activator.CreateInstance(fi.FieldType, ctorAttribute.Parameters);
                        });

                        if(field.Field.FieldType.IsValueType)
                        {
                            context.Generator.Emit(OpCodes.Unbox_Any, field.Field.FieldType);
                        }

                        context.Generator.Emit(OpCodes.Stfld, field.Field);
                    }
                    continue;
                }

                context.Generator.PushLocalAddressOntoStack(structLocal);
                var type = field.TypeToOmit ?? field.Field.FieldType;

                GenerateReadField(context, type, false);
                if(field.Field != null)
                {
                    context.Generator.Emit(OpCodes.Stfld, field.Field);
                }
                else
                {
                    context.Generator.Emit(OpCodes.Pop); // struct local
                    context.Generator.Emit(OpCodes.Pop); // read value
                }
            }
        }

        private static void GenerateReadArray(ReaderGenerationContext context, Type arrayType, Action pushObjectIdOntoStackAction)
        {
            var isMultidimensional = arrayType.GetArrayRank() > 1;
            var elementFormalType = arrayType.GetElementType();

            var rankLocal = context.Generator.DeclareLocal(typeof(int));
            var lengthsLocal = isMultidimensional ? context.Generator.DeclareLocal(typeof(int[])) : context.Generator.DeclareLocal(typeof(int));
            var arrayLocal = context.Generator.DeclareLocal(typeof(Array));
            var positionLocal = isMultidimensional ? context.Generator.DeclareLocal(typeof(int[])) : context.Generator.DeclareLocal(typeof(int));
            var loopControlLocal = context.Generator.DeclareLocal(typeof(int)); // type is int not bool to reuse array length directly
            var loopBeginLabel = context.Generator.DefineLabel();
            var loopEndLabel = context.Generator.DefineLabel();
            var nonZeroLengthLabel = context.Generator.DefineLabel();

            context.PushDeserializedObjectOntoStack(pushObjectIdOntoStackAction);
            context.Generator.Emit(OpCodes.Castclass, typeof(Array));
            context.Generator.Emit(OpCodes.Dup);
            context.Generator.StoreLocalValueFromStack(arrayLocal);
            context.Generator.PushPropertyValueOntoStack<Array, int>(x => x.Rank);
            context.Generator.StoreLocalValueFromStack(rankLocal);

            if(isMultidimensional)
            {
                context.Generator.Emit(OpCodes.Ldc_I4_1);
                context.Generator.StoreLocalValueFromStack(loopControlLocal);

                context.Generator.PushLocalValueOntoStack(rankLocal);
                context.Generator.Emit(OpCodes.Newarr, typeof(int));
                context.Generator.StoreLocalValueFromStack(lengthsLocal); // create an array for keeping the lengths of each dimension

                GeneratorHelper.GenerateLoop(context, rankLocal, i =>
                {
                    context.Generator.PushLocalValueOntoStack(lengthsLocal);
                    context.Generator.PushLocalValueOntoStack(i);

                    context.Generator.PushLocalValueOntoStack(arrayLocal);
                    context.Generator.PushLocalValueOntoStack(i);
                    context.Generator.Call<Array>(x => x.GetLength(0));

                    context.Generator.Emit(OpCodes.Dup);
                    context.Generator.Emit(OpCodes.Brtrue, nonZeroLengthLabel);

                    context.Generator.Emit(OpCodes.Ldc_I4_0);
                    context.Generator.StoreLocalValueFromStack(loopControlLocal);

                    context.Generator.MarkLabel(nonZeroLengthLabel);
                    context.Generator.Emit(OpCodes.Stelem, typeof(int)); // populate the lengths with values read from stream
                });
            }
            else
            {
                context.Generator.PushLocalValueOntoStack(arrayLocal);
                context.Generator.Emit(OpCodes.Ldc_I4_0);
                context.Generator.Call<Array>(x => x.GetLength(0));
                context.Generator.Emit(OpCodes.Dup);
                context.Generator.StoreLocalValueFromStack(lengthsLocal);
                context.Generator.StoreLocalValueFromStack(loopControlLocal);
            }

            if(isMultidimensional)
            {
                context.Generator.PushLocalValueOntoStack(rankLocal);
                context.Generator.Emit(OpCodes.Newarr, typeof(int));
                context.Generator.StoreLocalValueFromStack(positionLocal); // create an array for keeping the current position of each dimension
            }

            context.Generator.MarkLabel(loopBeginLabel);
            context.Generator.PushLocalValueOntoStack(loopControlLocal);
            context.Generator.Emit(OpCodes.Brfalse, loopEndLabel);

            context.Generator.PushLocalValueOntoStack(arrayLocal);
            context.Generator.Emit(OpCodes.Castclass, arrayType);
            if(isMultidimensional)
            {
                GenerateReadField(context, elementFormalType);
                context.Generator.PushLocalValueOntoStack(positionLocal);
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Array>(a => a.SetValue(null, new int[0])));
            }
            else
            {
                context.Generator.PushLocalValueOntoStack(positionLocal);
                context.Generator.Emit(OpCodes.Ldelema, elementFormalType);

                GenerateReadField(context, elementFormalType, false);
                context.Generator.Emit(OpCodes.Stobj, elementFormalType);
            }

            if(isMultidimensional)
            {
                context.Generator.PushLocalValueOntoStack(positionLocal);
                context.Generator.PushLocalValueOntoStack(lengthsLocal);
                context.Generator.PushLocalValueOntoStack(rankLocal);

                context.Generator.GenerateCodeFCall<int[], int[], int, bool>((counter, sizes, ranks) =>
                {
                    var currentRank = ranks - 1;

                    while(currentRank >= 0)
                    {
                        counter[currentRank]++;
                        if(counter[currentRank] < sizes[currentRank])
                        {
                            return true;
                        }

                        counter[currentRank] = 0;
                        currentRank--;
                    }

                    return false;
                });
            }
            else
            {
                context.Generator.PushLocalValueOntoStack(positionLocal);
                context.Generator.Emit(OpCodes.Ldc_I4_1);
                context.Generator.Emit(OpCodes.Add);
                context.Generator.Emit(OpCodes.Dup);
                context.Generator.StoreLocalValueFromStack(positionLocal);
                context.Generator.PushLocalValueOntoStack(lengthsLocal);
                context.Generator.Emit(OpCodes.Clt);
            }

            context.Generator.StoreLocalValueFromStack(loopControlLocal);

            context.Generator.Emit(OpCodes.Br, loopBeginLabel);
            context.Generator.MarkLabel(loopEndLabel);
        }

        private static void GenerateReadDelegate(ReaderGenerationContext context, Type type, Action pushObjectIdOntoStackAction)
        {
            var invocationListLengthLocal = context.Generator.DeclareLocal(typeof(int));
            var targetLocal = context.Generator.DeclareLocal(typeof(object));

            GenerateReadPrimitive(context, typeof(int));
            context.Generator.StoreLocalValueFromStack(invocationListLengthLocal);

            GeneratorHelper.GenerateLoop(context, invocationListLengthLocal, cl =>
            {
                GenerateReadField(context, typeof(object));
                context.Generator.StoreLocalValueFromStack(targetLocal);

                GenerateReadMethod(context);
                context.Generator.PushTypeOntoStack(type);
                context.Generator.PushLocalValueOntoStack(targetLocal);
                context.PushDeserializedObjectsCollectionOntoStack();
                pushObjectIdOntoStackAction();

                context.Generator.GenerateCodeCall<MethodInfo, Type, object, AutoResizingList<object>, int>((method, t, target, deserializedObjects, objectId) =>
                {
                    var del = Delegate.CreateDelegate(t, target, method);
                    deserializedObjects[objectId] = Delegate.Combine((Delegate)deserializedObjects[objectId], del);
                });
            });
        }

        private static void GenerateReadMethod(ReaderGenerationContext context)
        {
            // method returns read methodInfo (or null)

            context.PushObjectReaderOntoStack();
            context.Generator.Emit(OpCodes.Call, Helpers.GetPropertyGetterInfo<ObjectReader, IdentifiedElementsList<MethodDescriptor>>(or => or.Methods));
            context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<IdentifiedElementsList<MethodDescriptor>, MethodDescriptor>(or => or.Read()));
            context.Generator.Emit(OpCodes.Call, Helpers.GetPropertyGetterInfo<MethodDescriptor, MethodInfo>(md => md.UnderlyingMethod));
        }

        private const string InternalErrorMessage = "Internal error: should not reach here.";
        private const string CouldNotFindAddErrorMessage = "Could not find suitable Add method for the type {0}.";
    }
}
