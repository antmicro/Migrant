/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

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
using AntMicro.Migrant.Hooks;

namespace AntMicro.Migrant.Generators
{
	internal class WriteMethodGenerator
	{
		internal WriteMethodGenerator(Type typeToGenerate, IDictionary<Type, int> typeIndices)
		{
			InitializeMethodInfos();
			this.typeIndices = typeIndices;
			if(!typeToGenerate.IsArray)
			{
				dynamicMethod = new DynamicMethod("Write", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard,
			                               typeof(void), ParameterTypes, typeToGenerate, true);
			}
			else
			{
				var methodNo = Interlocked.Increment(ref WriteArrayMethodCounter);
				dynamicMethod = new DynamicMethod(string.Format("WriteArray{0}", methodNo), null, ParameterTypes, true);
			}
			generator = dynamicMethod.GetILGenerator();

			// preserialization callbacks
			GenerateInvokeCallbacks(typeToGenerate, typeof(PreSerializationAttribute));

			if(!GenerateSpecialWrite(typeToGenerate))
			{
				GenerateWriteFields(gen =>
				                    {
					gen.Emit(OpCodes.Ldarg_2);
				}, typeToGenerate);
			}

			// postserialization callbacks
			GenerateInvokeCallbacks(typeToGenerate, typeof(PostSerializationAttribute));

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

		private void GenerateInvokeCallbacks(Type actualType, Type attributeType)
		{
			var preSerializationMethods = Helpers.GetMethodsWithAttribute(attributeType, actualType);
			foreach(var method in preSerializationMethods)
			{
				if(!method.IsStatic)
				{
					generator.Emit(OpCodes.Ldarg_2); // object to serialize
				}
				generator.Emit(OpCodes.Call, method);
			}
		}

		private void GenerateWriteFields(Action<ILGenerator> putValueToWriteOnTop, Type actualType)
		{
			var fields = ObjectWriter.GetFieldsInSerializationOrder(actualType);
			foreach(var field in fields)
			{
				GenerateWriteType(gen => 
				                  {
					putValueToWriteOnTop(gen);
					gen.Emit(OpCodes.Ldfld, field);
				}, field.FieldType);
			}
		}

		private bool GenerateSpecialWrite(Type actualType)
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
				}, actualType);
				return true;
			}
			bool isGeneric, isGenericallyIterable, isDictionary;
			Type elementType;
			if(Helpers.IsCollection(actualType, out elementType, out isGeneric, out isGenericallyIterable, out isDictionary))
			{
				GenerateWriteCollection(elementType, isGeneric, isGenericallyIterable, isDictionary);
				return true;
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

			// writing elements
			generator.Emit(OpCodes.Ldc_I4_0);
			generator.Emit(OpCodes.Stloc_0);
			var loopBegin = generator.DefineLabel();
			generator.MarkLabel(loopBegin);
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
			generator.Emit(OpCodes.Ldloc_0);
			generator.Emit(OpCodes.Ldloc_2); // length of the array
			generator.Emit(OpCodes.Blt, loopBegin);
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
		}

		private void GenerateWriteCollection(Type formalElementType, bool isGeneric, bool isGenericallyIterable, bool isIDictionary)
		{
			var genericTypes = new [] { formalElementType };
			var ifaceType = isGeneric ? typeof(ICollection<>).MakeGenericType(genericTypes) : typeof(ICollection);
			Type enumerableType;
			if(isIDictionary)
			{
				formalElementType = typeof(object); // convenient in our case
				enumerableType = typeof(IDictionary);
			}
			else
			{
				enumerableType = isGenericallyIterable ? typeof(IEnumerable<>).MakeGenericType(genericTypes) : typeof(IEnumerable);
			}
			Type enumeratorType;
			if(isIDictionary)
			{
				enumeratorType = typeof(IDictionaryEnumerator);
			}
			else
			{
				enumeratorType = isGenericallyIterable ? typeof(IEnumerator<>).MakeGenericType(genericTypes) : typeof(IEnumerator);
			}

			generator.DeclareLocal(enumeratorType); // iterator
			generator.DeclareLocal(formalElementType); // current element

			// length of the collection
			generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
			generator.Emit(OpCodes.Ldarg_2); // collection to serialize
			var countMethod = ifaceType.GetProperty("Count").GetGetMethod();
			generator.Emit(OpCodes.Call, countMethod);
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
			if(isIDictionary)
			{
				// key
				generator.Emit(OpCodes.Call, enumeratorType.GetProperty("Key").GetGetMethod());
				generator.Emit(OpCodes.Stloc_1);
				GenerateWriteTypeLocal1(generator, formalElementType);

				// value
				generator.Emit(OpCodes.Ldloc_0);
				generator.Emit(OpCodes.Call, enumeratorType.GetProperty("Value").GetGetMethod());
				generator.Emit(OpCodes.Stloc_1);
				GenerateWriteTypeLocal1(generator, formalElementType);
			}
			else
			{
				generator.Emit(OpCodes.Call, enumeratorType.GetProperty("Current").GetGetMethod());
				generator.Emit(OpCodes.Stloc_1);
	
				// operation on current element
				GenerateWriteTypeLocal1(generator, formalElementType);
			}

			generator.Emit(OpCodes.Br, loopBegin);
			generator.MarkLabel(finish);
		}

		private void GenerateWriteTypeLocal1(ILGenerator generator, Type formalElementType)
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

		private void GenerateWriteDelegate(Action<ILGenerator> putValueToWriteOnTop, Type formalType)
		{
			var delegateGetMethodInfo = typeof(MulticastDelegate).GetProperty("Method").GetGetMethod();
			var delegateGetTarget = typeof(MulticastDelegate).GetProperty("Target").GetGetMethod();
			var delegateGetInvocationList = Helpers.GetMethodInfo<MulticastDelegate>(md => md.GetInvocationList());
			var memberInfoGetMetadataToken = typeof(MemberInfo).GetProperty("MetadataToken").GetGetMethod();

			var loopCounter = generator.DeclareLocal(typeof(int));
			var array = generator.DeclareLocal(typeof(Delegate[]));
			var element = generator.DeclareLocal(typeof(Delegate));
			var loopLength = generator.DeclareLocal(typeof(int));
			generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
			putValueToWriteOnTop(generator);
			generator.Emit(OpCodes.Callvirt, delegateGetInvocationList);
			generator.Emit(OpCodes.Castclass, typeof(Delegate[]));
			generator.Emit(OpCodes.Dup);
			generator.Emit(OpCodes.Stloc_S, array.LocalIndex);
			generator.Emit(OpCodes.Ldlen);
			generator.Emit(OpCodes.Dup);
			generator.Emit(OpCodes.Stloc_S, loopLength.LocalIndex);
			generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

			generator.Emit(OpCodes.Ldc_I4_0);
			generator.Emit(OpCodes.Stloc_S, loopCounter.LocalIndex);

			var loopBegin = generator.DefineLabel();
			generator.MarkLabel(loopBegin);
			generator.Emit(OpCodes.Ldloc, array.LocalIndex);
			generator.Emit(OpCodes.Ldloc, loopCounter.LocalIndex);
			generator.Emit(OpCodes.Ldelem, element.LocalType);
			generator.Emit(OpCodes.Stloc_S, element.LocalIndex);
			GenerateWriteReference(gen =>
			                       {
				generator.Emit(OpCodes.Ldloc_S, element.LocalIndex);
				gen.Emit(OpCodes.Call, delegateGetTarget);
			}, typeof(object));

			generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
			generator.Emit(OpCodes.Ldloc_S, element.LocalIndex);
			generator.Emit(OpCodes.Call, delegateGetMethodInfo);
			generator.Emit(OpCodes.Call, memberInfoGetMetadataToken);
			generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);

			generator.Emit(OpCodes.Ldloc_S, loopCounter.LocalIndex);
			generator.Emit(OpCodes.Ldc_I4_1);
			generator.Emit(OpCodes.Add);
			generator.Emit(OpCodes.Dup);
			generator.Emit(OpCodes.Stloc_S, loopCounter.LocalIndex);
			generator.Emit(OpCodes.Ldloc_S, loopLength.LocalIndex);
			generator.Emit(OpCodes.Blt, loopBegin);
		}

		private void GenerateWriteValue(Action<ILGenerator> putValueToWriteOnTop, Type formalType)
		{
			ObjectWriter.CheckLegality(formalType);

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
				return;
			}
			GenerateWriteFields(putValueToWriteOnTop, formalType);
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

			var formalTypeIsActualType = formalType.Attributes.HasFlag(TypeAttributes.Sealed); // TODO: more optimizations?
			// maybe we know the type id yet?
			if(formalTypeIsActualType && typeIndices.ContainsKey(formalType))
			{
				var typeId = typeIndices[formalType];
				generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
				generator.Emit(OpCodes.Ldc_I4, typeId);
				generator.Emit(OpCodes.Call, primitiveWriterWriteInteger);
			}
			else
			{
				// we have to get the actual type at runtime
				generator.Emit(OpCodes.Ldarg_0); // objectWriter
				putValueToWriteOnTop(generator);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter, object>((writer, obj) => writer.TouchAndWriteTypeId(obj)));
			}

			// TODO: other opts here?
			// if there is possibity that the target object is transient, we have to check that
			var skipGetId = false;
			var skipTransientCheck = false;
			if(formalTypeIsActualType)
			{
				if(formalType.IsDefined(typeof(TransientAttribute), false))
				{
					skipGetId = true;
				}
				else
				{
					skipTransientCheck = true;
				}
			}

			if(!skipTransientCheck)
			{
				generator.Emit(OpCodes.Ldarg_0); // objectWriter
				putValueToWriteOnTop(generator); // value to serialize
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter, object>((writer, obj) => writer.CheckTransient(obj)));
				generator.Emit(OpCodes.Brtrue_S, finish);
			}

			if(!skipGetId)
			{
				// if the formal type is NOT object, then string or array will not be the content of the field
				// TODO: what with the abstract Array type?
				var mayBeInlined = formalType == typeof(object) || Helpers.CanBeCreatedWithDataOnly(formalType);
				generator.Emit(OpCodes.Ldarg_0); // objectWriter
				putValueToWriteOnTop(generator);
				if(mayBeInlined)
				{
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter>(writer => writer.WriteObjectIdPossiblyInline(null)));
				}
				else
				{
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectWriter>(writer => writer.WriteObjectId(null)));
				}
			}
			generator.MarkLabel(finish);
		}

		private MethodInfo primitiveWriterWriteInteger;
		private MethodInfo primitiveWriterWriteBoolean;

		private readonly IDictionary<Type, int> typeIndices;
		private readonly ILGenerator generator;
		private readonly DynamicMethod dynamicMethod;

		private static int WriteArrayMethodCounter;
		private static readonly Type[] ParameterTypes = new [] { typeof(ObjectWriter), typeof(PrimitiveWriter), typeof(object) };
	}
}

