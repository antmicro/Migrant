/*
  Copyright (c) 2012 - 2015 Antmicro <www.antmicro.com>

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
    internal class WriteMethodGenerator
    {
        internal WriteMethodGenerator(Type typeToGenerate, bool treatCollectionAsUserObject, int surrogateId, FieldInfo surrogatesField, MethodInfo writeObjectMethod)
        {
            typeWeAreGeneratingFor = typeToGenerate;
            ObjectWriter.CheckLegality(typeToGenerate);
            InitializeMethodInfos();
            if(!typeToGenerate.IsArray)
            {
                dynamicMethod = new DynamicMethod(string.Format("Write_{0}", typeToGenerate.Name), MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard,
                    typeof(void), ParameterTypes, typeToGenerate, true);
            }
            else
            {
                var methodNo = Interlocked.Increment(ref WriteArrayMethodCounter);
                dynamicMethod = new DynamicMethod(string.Format("WriteArray{0}_{1}", methodNo, typeToGenerate.Name), null, ParameterTypes, true);
            }
            generator = dynamicMethod.GetILGenerator();

            // surrogates
            if(surrogateId != -1)
            {
                generator.Emit(OpCodes.Ldarg_0); // used (1)

                // obtain surrogate factory
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, surrogatesField);
                generator.Emit(OpCodes.Ldc_I4, surrogateId);
                generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<SwapList>(x => x.GetByIndex(0)));

                // call surrogate factory to obtain surrogate object
                var delegateType = typeof(Func<,>).MakeGenericType(typeToGenerate, typeof(object));
                generator.Emit(OpCodes.Castclass, delegateType);
                generator.Emit(OpCodes.Ldarg_2);
                generator.Emit(OpCodes.Castclass, typeToGenerate);
                generator.Emit(OpCodes.Call, delegateType.GetMethod("Invoke"));

                // jump to surrogate serialization
                generator.Emit(OpCodes.Call, writeObjectMethod); // (1) here
                generator.Emit(OpCodes.Ret);
                return;
            }

            var delegateTouchAndWriteTypeId = Helpers.GetMethodInfo<ObjectWriter, Type>((writer, type) => writer.TouchAndWriteTypeId(type));
            generator.Emit(OpCodes.Ldarg_0); // objectWriter

            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object>(o => o.GetType()));

            generator.Emit(OpCodes.Call, delegateTouchAndWriteTypeId);
            generator.Emit(OpCodes.Pop);

            // preserialization callbacks
            GenerateInvokeCallback(typeToGenerate, typeof(PreSerializationAttribute));
            var exceptionBlockNeeded = Helpers.GetMethodsWithAttribute(typeof(PostSerializationAttribute), typeToGenerate).Any() ||
                                       Helpers.GetMethodsWithAttribute(typeof(LatePostSerializationAttribute), typeToGenerate).Any();
            if(exceptionBlockNeeded)
            {
                generator.BeginExceptionBlock();
            }

            if(!GenerateSpecialWrite(typeToGenerate, treatCollectionAsUserObject))
            {
                GenerateWriteFields(gen =>
                {
                    gen.Emit(OpCodes.Ldarg_2);
                }, typeToGenerate);
            }
            if(exceptionBlockNeeded)
            {
                generator.BeginFinallyBlock();
            }
            // postserialization callbacks
            GenerateInvokeCallback(typeToGenerate, typeof(PostSerializationAttribute));
            GenerateAddCallbackToInvokeList(typeToGenerate, typeof(LatePostSerializationAttribute));
            if(exceptionBlockNeeded)
            {
                generator.EndExceptionBlock();
            }
            generator.Emit(OpCodes.Ret);
        }

        internal DynamicMethod Method
        {
            get
            {
                return dynamicMethod;
            }
        }

        private void InitializeMethodInfos()
        {
            primitiveWriterWriteInteger = Helpers.GetMethodInfo<PrimitiveWriter>(writer => writer.Write(0));
            primitiveWriterWriteBoolean = Helpers.GetMethodInfo<PrimitiveWriter>(writer => writer.Write(false));
        }

        private void GenerateInvokeCallback(Type actualType, Type attributeType)
        {
            var methodsWithAttribute = Helpers.GetMethodsWithAttribute(attributeType, actualType);
            foreach(var method in methodsWithAttribute)
            {
                if(!method.IsStatic)
                {
                    generator.Emit(OpCodes.Ldarg_2); // object to serialize
                }
                if(method.IsVirtual)
                {
                    generator.Emit(OpCodes.Callvirt, method);
                }
                else
                {
                    generator.Emit(OpCodes.Call, method);
                }
            }
        }

        private void GenerateAddCallbackToInvokeList(Type actualType, Type attributeType)
        {
            var actionCtor = typeof(Action).GetConstructor(new [] { typeof(object), typeof(IntPtr) });
            var stackPush = Helpers.GetMethodInfo<List<Action>>(x => x.Add(null));

            var methodsWithAttribute = Helpers.GetMethodsWithAttribute(attributeType, actualType).ToList();
            var count = methodsWithAttribute.Count;
            if(count > 0)
            {
                generator.Emit(OpCodes.Ldarg_0); // objectWriter
                generator.Emit(OpCodes.Ldfld, typeof(ObjectWriter).GetField("postSerializationHooks", BindingFlags.NonPublic | BindingFlags.Instance)); // invoke list
            }
            for(var i = 1; i < count; i++)
            {
                generator.Emit(OpCodes.Dup);
            }
            foreach(var method in methodsWithAttribute)
            {
                // let's make the delegate
                if(method.IsStatic)
                {
                    generator.Emit(OpCodes.Ldnull);
                }
                else
                {
                    generator.Emit(OpCodes.Ldarg_2); // serialized object
                }
                generator.Emit(OpCodes.Ldftn, method);
                generator.Emit(OpCodes.Newobj, actionCtor);
                // and add it to invoke list
                generator.Emit(OpCodes.Call, stackPush);
            }
        }

        private void GenerateWriteFields(Action<ILGenerator> putValueToWriteOnTop, Type actualType)
        {
            var fields = StampHelpers.GetFieldsInSerializationOrder(actualType);
            foreach(var field in fields)
            {
                GenerateWriteType(gen =>
                {
                    putValueToWriteOnTop(gen);
                    if(!actualType.IsValueType)
                    {
                        gen.Emit(OpCodes.Castclass, actualType);
                    }
                    gen.Emit(OpCodes.Ldfld, field);
                }, field.FieldType);
            }
        }

        private bool GenerateSpecialWrite(Type actualType, bool treatCollectionAsUserObject)
        {
            if(actualType.IsValueType)
            {
                // value type encountered here means it is in fact boxed value type
                // according to protocol it is written as it would be written inlined
                GenerateWriteValue(gen =>
                {
                    gen.Emit(OpCodes.Ldarg_2); // value to serialize
                    gen.Emit(OpCodes.Unbox_Any, actualType);
                }, actualType);
                return true;
            }
            if(actualType.IsArray)
            {
                GenerateWriteArray(actualType);
                return true;
            }
            if(typeof(MulticastDelegate).IsAssignableFrom(actualType))
            {
                GenerateWriteDelegate(gen =>
                {
                    gen.Emit(OpCodes.Ldarg_2); // value to serialize
                });
                return true;
            }
            if(!treatCollectionAsUserObject)
            {
                CollectionMetaToken collectionToken;
                if(CollectionMetaToken.TryGetCollectionMetaToken(actualType, out collectionToken))
                {
                    GenerateWriteCollection(collectionToken);
                    return true;
                }
            }
            return false;
        }

        private void GenerateWriteArray(Type actualType)
        {
            var elementType = actualType.GetElementType();
            var rank = actualType.GetArrayRank();
            if(rank != 1)
            {
                GenerateWriteMultidimensionalArray(actualType, elementType);
                return;
            }

            generator.DeclareLocal(typeof(int)); // this is for counter
            generator.DeclareLocal(elementType); // this is for the current element
            generator.DeclareLocal(typeof(int)); // length of the array

            // writing rank
            generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

            // writing length
            generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
            generator.Emit(OpCodes.Ldarg_2); // array to serialize
            generator.Emit(OpCodes.Castclass, actualType);
            generator.Emit(OpCodes.Ldlen);
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Stloc_2);
            generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

            var loopEnd = generator.DefineLabel();
            var loopBegin = generator.DefineLabel();
            // writing elements
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Stloc_0);

            generator.MarkLabel(loopBegin);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_2); // length of the array
            generator.Emit(OpCodes.Bge, loopEnd);

            generator.Emit(OpCodes.Ldarg_2); // array to serialize
            generator.Emit(OpCodes.Castclass, actualType);
            generator.Emit(OpCodes.Ldloc_0); // index
            generator.Emit(OpCodes.Ldelem, elementType);
            generator.Emit(OpCodes.Stloc_1); // we put current element to local variable

            GenerateWriteType(gen =>
            {
                gen.Emit(OpCodes.Ldloc_1); // current element
            }, elementType);

            // loop book keeping
            generator.Emit(OpCodes.Ldloc_0); // current index, which will be increased by 1
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
            generator.Emit(OpCodes.Br, loopBegin);
            generator.MarkLabel(loopEnd);
        }

        private void GenerateWriteMultidimensionalArray(Type actualType, Type elementType)
        {
            var rank = actualType.GetArrayRank();
            // local for current element
            generator.DeclareLocal(elementType);
            // locals for indices
            var indexLocals = new int[rank];
            for(var i = 0; i < rank; i++)
            {
                indexLocals[i] = generator.DeclareLocal(typeof(int)).LocalIndex;
            }
            // locals for lengths
            var lengthLocals = new int[rank];
            for(var i = 0; i < rank; i++)
            {
                lengthLocals[i] = generator.DeclareLocal(typeof(int)).LocalIndex;
            }

            // writing rank
            generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
            generator.Emit(OpCodes.Ldc_I4, rank);
            generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

            // writing lengths
            for(var i = 0; i < rank; i++)
            {
                generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
                generator.Emit(OpCodes.Ldarg_2); // array to serialize
                generator.Emit(OpCodes.Castclass, actualType);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Array>(array => array.GetLength(0)));
                generator.Emit(OpCodes.Dup);
                generator.Emit(OpCodes.Stloc, lengthLocals[i]);
                generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);
            }

            // writing elements
            GenerateArrayWriteLoop(0, rank, indexLocals, lengthLocals, actualType, elementType);
        }

        private void GenerateArrayWriteLoop(int currentDimension, int rank, int[] indexLocals, int[] lengthLocals, Type arrayType, Type elementType)
        {
            var arrayWrittenLabel = generator.DefineLabel();

            // check all the lengths and return if at least one is zero
            for(var i = 0; i < rank; i++)
            {
                generator.Emit(OpCodes.Ldloc, lengthLocals[i]);
                generator.Emit(OpCodes.Brfalse, arrayWrittenLabel);
            }

            // initalization
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Stloc, indexLocals[currentDimension]);
            var loopBegin = generator.DefineLabel();
            generator.MarkLabel(loopBegin);
            if(currentDimension == rank - 1)
            {
                // writing the element
                generator.Emit(OpCodes.Ldarg_2); // array to serialize
                generator.Emit(OpCodes.Castclass, arrayType);
                for(var i = 0; i < rank; i++)
                {
                    generator.Emit(OpCodes.Ldloc, indexLocals[i]);
                }
                generator.Emit(OpCodes.Call, arrayType.GetMethod("Get"));
                generator.Emit(OpCodes.Stloc_0);
                GenerateWriteType(gen => gen.Emit(OpCodes.Ldloc_0), elementType);
            }
            else
            {
                GenerateArrayWriteLoop(currentDimension + 1, rank, indexLocals, lengthLocals, arrayType, elementType);
            }
            // incremeting index and loop exit condition check
            generator.Emit(OpCodes.Ldloc, indexLocals[currentDimension]);
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Stloc, indexLocals[currentDimension]);
            generator.Emit(OpCodes.Ldloc, lengthLocals[currentDimension]);
            generator.Emit(OpCodes.Blt, loopBegin);

            generator.MarkLabel(arrayWrittenLabel);
        }

        private void GenerateWriteCollection(CollectionMetaToken token)
        {
            Type enumerableType;
            Type enumeratorType;
            if(token.IsDictionary)
            {
                enumerableType = typeof(IDictionary);
                enumeratorType = typeof(IDictionaryEnumerator);
            }
            else
            {
                var genericTypes = new [] { token.FormalElementType };

                enumerableType = token.IsGenericallyIterable ? typeof(IEnumerable<>).MakeGenericType(genericTypes) : typeof(IEnumerable);
                enumeratorType = token.IsGenericallyIterable ? typeof(IEnumerator<>).MakeGenericType(genericTypes) : typeof(IEnumerator);
            }

            generator.DeclareLocal(enumeratorType); // iterator
            if(token.IsDictionary)
            {
                generator.DeclareLocal(token.FormalKeyType);
                generator.DeclareLocal(token.FormalValueType);
            }
            else
            {
                generator.DeclareLocal(token.FormalElementType);
            }

            // length of the collection
            generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
            generator.Emit(OpCodes.Ldarg_2); // collection to serialize
            if(token.CountMethod.IsStatic)
            {
                generator.Emit(OpCodes.Call, token.CountMethod);
            }
            else
            {
                generator.Emit(OpCodes.Callvirt, token.CountMethod);
            }
            generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

            // elements
            var getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
            generator.Emit(OpCodes.Ldarg_2); // collection to serialize
            generator.Emit(OpCodes.Call, getEnumeratorMethod);
            generator.Emit(OpCodes.Stloc_0);
            var loopBegin = generator.DefineLabel();
            generator.MarkLabel(loopBegin);
            generator.Emit(OpCodes.Ldloc_0);
            var finish = generator.DefineLabel();
            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<IEnumerator>(x => x.MoveNext()));
            generator.Emit(OpCodes.Brfalse, finish);
            generator.Emit(OpCodes.Ldloc_0);
            if(token.IsDictionary)
            {
                // key
                generator.Emit(OpCodes.Nop);
                generator.Emit(OpCodes.Call, enumeratorType.GetProperty("Key").GetGetMethod());
                generator.Emit(OpCodes.Unbox_Any, token.FormalKeyType);
                generator.Emit(OpCodes.Stloc_1);
                GenerateWriteType(gen => gen.Emit(OpCodes.Ldloc_1), token.FormalKeyType);

                // value
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Call, enumeratorType.GetProperty("Value").GetGetMethod());
                generator.Emit(OpCodes.Unbox_Any, token.FormalValueType);
                generator.Emit(OpCodes.Stloc_2);
                GenerateWriteType(gen => gen.Emit(OpCodes.Ldloc_2), token.FormalValueType);
            }
            else
            {
                generator.Emit(OpCodes.Call, enumeratorType.GetProperty("Current").GetGetMethod());
                generator.Emit(OpCodes.Stloc_1);
	
                // operation on current element
                GenerateWriteTypeLocal1(token.FormalElementType);
            }

            generator.Emit(OpCodes.Br, loopBegin);
            generator.MarkLabel(finish);
        }

        private void GenerateWriteTypeLocal1(Type formalElementType)
        {
            GenerateWriteType(gen =>
            {
                gen.Emit(OpCodes.Ldloc_1);
            }, formalElementType);
        }

        private void GenerateWriteType(Action<ILGenerator> putValueToWriteOnTop, Type formalType)
        {
            switch(Helpers.GetSerializationType(formalType))
            {
            case SerializationType.Transient:
				// just omit it
                return;
            case SerializationType.Value:
                GenerateWriteValue(putValueToWriteOnTop, formalType);
                break;
            case SerializationType.Reference:
                GenerateWriteReference(putValueToWriteOnTop, formalType);
                break;
            }
        }

        private void GenerateWriteDelegate(Action<ILGenerator> putValueToWriteOnTop)
        {
            var delegateTouchAndWriteMethodId = Helpers.GetMethodInfo<ObjectWriter, MethodInfo>((writer, method) => writer.TouchAndWriteMethodId(method));
            var delegateGetInvocationList = Helpers.GetMethodInfo<ObjectWriter, MulticastDelegate>((writer, md) => writer.GetDelegatesWithNonTransientTargets(md));
            var delegateGetMethodInfo = typeof(MulticastDelegate).GetProperty("Method").GetGetMethod();
            var delegateGetTarget = typeof(MulticastDelegate).GetProperty("Target").GetGetMethod();

            var array = generator.DeclareLocal(typeof(Delegate[]));
            var loopLength = generator.DeclareLocal(typeof(int));
            var element = generator.DeclareLocal(typeof(Delegate));

            generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
            generator.Emit(OpCodes.Ldarg_0); // objectWriter
            putValueToWriteOnTop(generator); // delegate to serialize of type MulticastDelegate

            generator.Emit(OpCodes.Call, delegateGetInvocationList);
            generator.Emit(OpCodes.Castclass, typeof(Delegate[]));
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Stloc_S, array.LocalIndex);
            generator.Emit(OpCodes.Ldlen);
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Stloc_S, loopLength.LocalIndex);
            generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

            GeneratorHelper.GenerateLoop(generator, loopLength, c =>
            {
                generator.Emit(OpCodes.Ldloc, array);
                generator.Emit(OpCodes.Ldloc, c);
                generator.Emit(OpCodes.Ldelem, element.LocalType);
                generator.Emit(OpCodes.Stloc, element);

                GenerateWriteReference(gen =>
                {
                    gen.Emit(OpCodes.Ldloc, element);
                    gen.Emit(OpCodes.Call, delegateGetTarget);
                }, typeof(object));

                generator.Emit(OpCodes.Ldarg_0); // objectWriter
                generator.Emit(OpCodes.Ldloc, element);
                generator.Emit(OpCodes.Call, delegateGetMethodInfo);
                generator.Emit(OpCodes.Call, delegateTouchAndWriteMethodId);
                generator.Emit(OpCodes.Pop);
            });
        }

        private void GenerateWriteValue(Action<ILGenerator> putValueToWriteOnTop, Type formalType)
        {
            ObjectWriter.CheckLegality(formalType, typeWeAreGeneratingFor);
            if(formalType.IsEnum)
            {
                formalType = Enum.GetUnderlyingType(formalType);
            }
            var writeMethod = typeof(PrimitiveWriter).GetMethod("Write", new [] { formalType });
            // if this method is null, then it is a non-primitive (i.e. custom) struct
            if(writeMethod != null)
            {
                generator.Emit(OpCodes.Ldarg_1); // primitive writer waits there
                putValueToWriteOnTop(generator);
                generator.Emit(OpCodes.Call, writeMethod);
                return;
            }
            var nullableUnderlyingType = Nullable.GetUnderlyingType(formalType);
            if(nullableUnderlyingType != null)
            {
                var hasValue = generator.DefineLabel();
                var finish = generator.DefineLabel();
                var localIndex = generator.DeclareLocal(formalType).LocalIndex;
                generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
                putValueToWriteOnTop(generator);
                generator.Emit(OpCodes.Stloc_S, localIndex);
                generator.Emit(OpCodes.Ldloca_S, localIndex);
                generator.Emit(OpCodes.Call, formalType.GetProperty("HasValue").GetGetMethod());
                generator.Emit(OpCodes.Brtrue_S, hasValue);
                generator.Emit(OpCodes.Ldc_I4_0);
                generator.Emit(OpCodes.Call, primitiveWriterWriteBoolean);
                generator.Emit(OpCodes.Br, finish);
                generator.MarkLabel(hasValue);
                generator.Emit(OpCodes.Ldc_I4_1);
                generator.Emit(OpCodes.Call, primitiveWriterWriteBoolean);
                GenerateWriteValue(gen =>
                {
                    generator.Emit(OpCodes.Ldloca_S, localIndex);
                    generator.Emit(OpCodes.Call, formalType.GetProperty("Value").GetGetMethod());
                }, nullableUnderlyingType);
                generator.MarkLabel(finish);
                return;
            }

            if(formalType.IsGenericType && formalType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyValueTypes = formalType.GetGenericArguments();
                var localIndex = generator.DeclareLocal(formalType).LocalIndex;
                GenerateWriteType(gen =>
                {
                    putValueToWriteOnTop(gen);
                    // TODO: is there a better method of getting address?
                    // don't think so, looking at
                    // http://stackoverflow.com/questions/76274/
                    // we *may* do a little optimization if this value write takes
                    // place when dictionary is serialized (current KVP is stored in
                    // local 1 in such situation); the KVP may be, however, written
                    // independently
                    gen.Emit(OpCodes.Stloc_S, localIndex);
                    gen.Emit(OpCodes.Ldloca_S, localIndex);
                    gen.Emit(OpCodes.Call, formalType.GetProperty("Key").GetGetMethod());
                }, keyValueTypes[0]);
                GenerateWriteType(gen =>
                {
                    // we assume here that the key write was invoked earlier (it should be
                    // if we're conforming to the protocol), so KeyValuePair is already
                    // stored as local
                    gen.Emit(OpCodes.Ldloca_S, localIndex);
                    gen.Emit(OpCodes.Call, formalType.GetProperty("Value").GetGetMethod());
                }, keyValueTypes[1]);
            }
            else
            {
                GenerateWriteFields(putValueToWriteOnTop, formalType);
            }
        }

        private void GenerateWriteReference(Action<ILGenerator> putValueToWriteOnTop, Type formalType)
        {
            var finish = generator.DefineLabel();

            putValueToWriteOnTop(generator);
            var isNotNull = generator.DefineLabel();
            generator.Emit(OpCodes.Brtrue_S, isNotNull);
            generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
            generator.Emit(OpCodes.Ldc_I4, Consts.NullObjectId);
            generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);
            generator.Emit(OpCodes.Br, finish);
            generator.MarkLabel(isNotNull);

            var formalTypeIsActualType = (formalType.Attributes & TypeAttributes.Sealed) != 0;
            if(!formalTypeIsActualType)
            {
                generator.Emit(OpCodes.Ldarg_0); // objectWriter
                putValueToWriteOnTop(generator); // value to serialize
                generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter, object>((writer, obj) => writer.CheckTransient(obj)));
                var isNotTransient = generator.DefineLabel();
                generator.Emit(OpCodes.Brfalse_S, isNotTransient);
                generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
                generator.Emit(OpCodes.Ldc_I4, Consts.NullObjectId);
                generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);
                generator.Emit(OpCodes.Br, finish);
                generator.MarkLabel(isNotTransient);

                generator.Emit(OpCodes.Ldarg_0); // objectWriter
                putValueToWriteOnTop(generator);
                generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter>(writer => writer.WriteObjectIdPossiblyInline(null)));
            }
            else
            {
                if(Helpers.CheckTransientNoCache(formalType))
                {
                    generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
                    generator.Emit(OpCodes.Ldc_I4, Consts.NullObjectId);
                    generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);
                }
                else
                {
                    generator.Emit(OpCodes.Ldarg_0); // objectWriter
                    putValueToWriteOnTop(generator);
                    generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter>(writer => writer.WriteObjectIdPossiblyInline(null)));
                }
            }

            generator.MarkLabel(finish);
        }

        private MethodInfo primitiveWriterWriteInteger;
        private MethodInfo primitiveWriterWriteBoolean;
        private readonly ILGenerator generator;
        private readonly DynamicMethod dynamicMethod;
        private readonly Type typeWeAreGeneratingFor;
        private static int WriteArrayMethodCounter;
        private static readonly Type[] ParameterTypes = new [] {
            typeof(ObjectWriter),
            typeof(PrimitiveWriter),
            typeof(object)
        };
    }
}

