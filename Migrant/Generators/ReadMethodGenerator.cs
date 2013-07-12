/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

  Authors:
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
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.KKKK
*/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using ImpromptuInterface;
using AntMicro.Migrant;
using AntMicro.Migrant.Hooks;
using AntMicro.Migrant.Utilities;
using System.Collections;
using AntMicro.Migrant.VersionTolerance;

namespace Migrant.Generators
{
	internal sealed class ReadMethodGenerator
	{
		public ReadMethodGenerator(Type typeToGenerate, TypeStamper typeStamper)
		{
			this.typeStamper = typeStamper;
			if (typeToGenerate.IsArray)
			{
				dynamicMethod = new DynamicMethod("Read", typeof(object), ParameterTypes, true);
			}
			else
			{
				dynamicMethod = new DynamicMethod("Read", typeof(object), ParameterTypes, typeToGenerate, true);
			}
			generator = dynamicMethod.GetILGenerator();

			GenerateDynamicCode(typeToGenerate);
		}

		private void GenerateDynamicCode(Type typeToGenerate)
		{
			GenerateReadObjectInner(typeToGenerate);
			
			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0]));
					    
			generator.Emit(OpCodes.Ret);
		}

		private void GenerateReadPrimitive(Type type)
		{
			PushPrimitiveReaderOntoStack();
			var mname = string.Concat("Read", type.Name);
			var readMethod = typeof(PrimitiveReader).GetMethod(mname);
			if(readMethod == null)
			{
				throw new ArgumentException("Method <<" + mname + ">> not found");
			}

			generator.Emit(OpCodes.Call, readMethod);
		}

		private void GenerateReadNotPrecreated(Type formalType, LocalBuilder objectIdLocal)
		{
			if(formalType.IsValueType)
			{
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				GenerateReadField(formalType, true);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
			}
			else
			if(formalType == typeof(string))
			{
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				GenerateReadPrimitive(formalType);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
			}
			else
			if(formalType.IsArray)
			{
				GenerateReadArray(formalType.GetElementType(), objectIdLocal);
			}
			else
			if(typeof(MulticastDelegate).IsAssignableFrom(formalType))
			{
				GenereateReadDelegate(formalType, objectIdLocal);
			}
			else
			if(formalType.IsGenericType && typeof(ReadOnlyCollection<>).IsAssignableFrom(formalType.GetGenericTypeDefinition()))
			{
				GenerateReadReadOnlyCollection(formalType, objectIdLocal);
			}
			else
			{
				throw new InvalidOperationException(InternalErrorMessage + "GenerateReadNotPrecreated");
			}
		}

		private void GenereateReadDelegate(Type type, LocalBuilder objectIdLocal)
		{
			var invocationListLengthLocal = generator.DeclareLocal(typeof(Int32));
			var targetLocal = generator.DeclareLocal(typeof(object));
			var containingTypeLocal = generator.DeclareLocal(typeof(Type));
			var methodNumberLocal = generator.DeclareLocal(typeof(Int32));

			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, invocationListLengthLocal);

			GenerateLoop(invocationListLengthLocal, cl => {
				GenerateReadField(typeof(object));
				generator.Emit(OpCodes.Stloc, targetLocal);

				GenerateReadType();
				generator.Emit(OpCodes.Stloc, containingTypeLocal);

				GenerateReadPrimitive(typeof(Int32));
				generator.Emit(OpCodes.Stloc, methodNumberLocal);

				generator.Emit(OpCodes.Ldloc, containingTypeLocal);
				generator.Emit(OpCodes.Ldloc, methodNumberLocal);
				PushTypeOntoStack(type);
				generator.Emit(OpCodes.Ldloc, targetLocal);
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);

				GenerateCodeCall<Type, int, Type, object, AutoResizingList<object>, int>((containingType, methodNumber, t, target, deserializedObjects, objectId) => {
					// constructor cannot be bound to delegate, so we can just cast to methodInfo
					var method = (MethodInfo)containingType.Module.ResolveMethod(methodNumber);
					var del = Delegate.CreateDelegate(t, target, method);
					deserializedObjects[objectId] = Delegate.Combine((Delegate)deserializedObjects[objectId], del);
				});
			});
		}

		private void GenerateReadObjectInner(Type formalType)
		{
			var finish = generator.DefineLabel();
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));

			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Stloc, objectIdLocal);

			GenerateTouchObject(formalType);
			
			switch(ObjectReader.GetCreationWay(formalType))
			{
			case ObjectReader.CreationWay.Null:
				GenerateReadNotPrecreated(formalType, objectIdLocal);
				break;
			case ObjectReader.CreationWay.DefaultCtor:
				GenerateUpdateElements(formalType, objectIdLocal);
				break;
			case ObjectReader.CreationWay.Uninitialized:
				GenerateUpdateFields(formalType, objectIdLocal);
				break;
			}
			
			PushDeserializedObjectOntoStack(objectIdLocal);
			generator.Emit(OpCodes.Brfalse, finish);
			
			PushDeserializedObjectOntoStack(objectIdLocal);
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldloc, objectIdLocal);
			GenerateCodeCall<object, ObjectReader, int>((obj, or, objectId) => {
				Helpers.SwapObjectWithSurrogate(ref obj, or.objectsForSurrogates);
				or.deserializedObjects[objectId] = obj; // could be swapped
			});

			var methods = Helpers.GetMethodsWithAttribute(typeof(PostDeserializationAttribute), formalType);
			foreach(var method in methods)
			{
				PushDeserializedObjectOntoStack(objectIdLocal);
				generator.Emit(OpCodes.Castclass, method.ReflectedType);
				generator.Emit(OpCodes.Call, method);
			}
			
			methods = Helpers.GetMethodsWithAttribute(typeof(LatePostDeserializationAttribute), formalType);
			foreach(var method in methods)
			{
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Ldfld, Helpers.GetFieldInfo<ObjectReader, object>(or => or.postDeserializationHooks));

				PushTypeOntoStack(typeof(Action));
				if(method.IsStatic)
				{
					generator.Emit(OpCodes.Ldnull);
				}
				else
				{
					PushDeserializedObjectOntoStack(objectIdLocal);
					generator.Emit(OpCodes.Castclass, method.ReflectedType);
				}
				generator.Emit(OpCodes.Ldtoken, method);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, MethodBase>(x => MethodBase.GetMethodFromHandle(new RuntimeMethodHandle())));
				generator.Emit(OpCodes.Castclass, typeof(MethodInfo));
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, Delegate>(x => Delegate.CreateDelegate(null, null, method)));

				generator.Emit(OpCodes.Castclass, typeof(Action));

				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<List<Action>>(x => x.Add(null)));
			}

			generator.Emit(OpCodes.Ldarg_0);
			PushDeserializedObjectOntoStack(objectIdLocal);

			GenerateCodeCall<ObjectReader, object>((or, obj) => {
				if(or.postDeserializationCallback != null)
				{
					or.postDeserializationCallback(obj);
				}
			});
			
			generator.MarkLabel(finish);
		}

		private void GenerateUpdateElements(Type formalType, LocalBuilder objectIdLocal)
		{
			if(typeof(ISpeciallySerializable).IsAssignableFrom(formalType))
			{
				PushDeserializedObjectOntoStack(objectIdLocal);
				generator.Emit(OpCodes.Castclass, typeof(ISpeciallySerializable));
				PushPrimitiveReaderOntoStack();
				GenerateCodeCall<ISpeciallySerializable, PrimitiveReader>(ObjectReader.LoadAndVerifySpeciallySerializableAndVerify);
				return;
			}
			Type formalKeyType, formalValueType;
			bool isGenericDictionary;
			if(Helpers.IsDictionary(formalType, out formalKeyType, out formalValueType, out isGenericDictionary))
			{
				GenerateFillDictionary(formalKeyType, formalValueType, formalType, objectIdLocal);
				return;
			}
			Type elementFormalType;
			bool fake, fake2, fake3;
			if(!Helpers.IsCollection(formalType, out elementFormalType, out fake, out fake2, out fake3))
			{
				throw new InvalidOperationException(InternalErrorMessage);
			}

			GenerateFillCollection(elementFormalType, formalType, objectIdLocal);
		}

		private void GenerateLoop(LocalBuilder countLocal, Action<LocalBuilder> loopAction, bool reversed = false)
		{
			var loopControlLocal = generator.DeclareLocal(typeof(Int32));
			
			var loopLabel = generator.DefineLabel();
			var loopFinishLabel = generator.DefineLabel();

			if(reversed)
			{
				generator.Emit(OpCodes.Ldloc, countLocal);
				generator.Emit(OpCodes.Ldc_I4_1);
				generator.Emit(OpCodes.Sub); // put <<countLocal>> - 1 on stack
			}
			else
			{
				generator.Emit(OpCodes.Ldc_I4_0); // put <<0> on stack
			}

			generator.Emit(OpCodes.Stloc, loopControlLocal); // initialize <<loopControl>> variable using value from stack
			
			generator.MarkLabel(loopLabel);
			generator.Emit(OpCodes.Ldloc, loopControlLocal);

			if(reversed)
			{
				generator.Emit(OpCodes.Ldc_I4, -1);
			}
			else
			{
				generator.Emit(OpCodes.Ldloc, countLocal);

			}
			generator.Emit(OpCodes.Beq, loopFinishLabel);

			loopAction(loopControlLocal);

			generator.Emit(OpCodes.Ldloc, loopControlLocal);
			generator.Emit(OpCodes.Ldc_I4, reversed ? 1 : -1);
			generator.Emit(OpCodes.Sub);
			generator.Emit(OpCodes.Stloc, loopControlLocal); // change <<loopControl>> variable by one
			generator.Emit(OpCodes.Br, loopLabel); // jump to the next loop iteration
			
			generator.MarkLabel(loopFinishLabel);
		}

		#region Collections generators

		private void GenerateFillDictionary(Type formalKeyType, Type formalValueType, Type dictionaryType, LocalBuilder objectIdLocal)
		{
			var countLocal = generator.DeclareLocal(typeof(Int32));

			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, countLocal); // read dictionary elements count

			var addMethodArgumentTypes = new [] {
				formalKeyType,
				formalValueType
			};
			var addMethod = dictionaryType.GetMethod("Add", addMethodArgumentTypes) ??
				dictionaryType.GetMethod("TryAdd", addMethodArgumentTypes);
			if(addMethod == null)
			{
				throw new InvalidOperationException(string.Format(CouldNotFindAddErrorMessage, dictionaryType));
			}

			GenerateLoop(countLocal, lc => {
				PushDeserializedObjectOntoStack(objectIdLocal);
				generator.Emit(OpCodes.Castclass, dictionaryType);

				GenerateReadField(formalKeyType, false);
				GenerateReadField(formalValueType, false);
				generator.Emit(OpCodes.Callvirt, addMethod);
				if(addMethod.ReturnType != typeof(void))
				{
					generator.Emit(OpCodes.Pop); // remove returned unused value from stack
				}
			});
		}
				
		private void GenerateReadReadOnlyCollection(Type type, LocalBuilder objectIdLocal)
		{
			var elementFormalType = type.GetGenericArguments()[0];
			
			var lengthLocal = generator.DeclareLocal(typeof(Int32));
			var arrayLocal = generator.DeclareLocal(elementFormalType.MakeArrayType());
			
			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, lengthLocal); // read number of elements in the collection
			
			generator.Emit(OpCodes.Ldloc, lengthLocal);
			generator.Emit(OpCodes.Newarr, elementFormalType);
			generator.Emit(OpCodes.Stloc, arrayLocal); // create array
			
			GenerateLoop(lengthLocal, lc => {
				generator.Emit(OpCodes.Ldloc, arrayLocal);
				generator.Emit(OpCodes.Ldloc, lc);
				GenerateReadField(elementFormalType);
				generator.Emit(OpCodes.Stelem, elementFormalType);
			});
			
			SaveNewDeserializedObject(objectIdLocal, () => {
				generator.Emit(OpCodes.Ldloc, arrayLocal);
				generator.Emit(OpCodes.Castclass, typeof(IList<>).MakeGenericType(elementFormalType));
				generator.Emit(OpCodes.Newobj, type.GetConstructor(new [] { typeof(IList<>).MakeGenericType(elementFormalType) }));
			});
		}
				
		private void GenerateFillCollection(Type elementFormalType, Type collectionType, LocalBuilder objectIdLocal)
		{
			var countLocal = generator.DeclareLocal(typeof(Int32));

			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, countLocal); // read collection elements count
			
			var addMethod = collectionType.GetMethod("Add", new [] { elementFormalType }) ??
				collectionType.GetMethod("Enqueue", new [] { elementFormalType }) ??
				collectionType.GetMethod("Push", new [] { elementFormalType });
			if(addMethod == null)
			{
				throw new InvalidOperationException(string.Format(CouldNotFindAddErrorMessage, collectionType));
			}
			
			if(collectionType == typeof(Stack) || 
				(collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(Stack<>)))
			{
				var tempArrLocal = generator.DeclareLocal(elementFormalType.MakeArrayType());
				
				generator.Emit(OpCodes.Ldloc, countLocal);
				generator.Emit(OpCodes.Newarr, elementFormalType); 
				generator.Emit(OpCodes.Stloc, tempArrLocal); // creates temporal array
				
				GenerateLoop(countLocal, cl => {
					generator.Emit(OpCodes.Ldloc, tempArrLocal);
					generator.Emit(OpCodes.Ldloc, cl);
					GenerateReadField(elementFormalType, false);
					generator.Emit(OpCodes.Stelem, elementFormalType);
				});
				
				GenerateLoop(countLocal, cl => {
					PushDeserializedObjectOntoStack(objectIdLocal);
					generator.Emit(OpCodes.Castclass, collectionType);

					generator.Emit(OpCodes.Ldloc, tempArrLocal);
					generator.Emit(OpCodes.Ldloc, cl);
					generator.Emit(OpCodes.Ldelem, elementFormalType);
					generator.Emit(OpCodes.Callvirt, collectionType.GetMethod("Push"));
				}, true);
			}
			else
			{
				GenerateLoop(countLocal, cl => {

					PushDeserializedObjectOntoStack(objectIdLocal);
					generator.Emit(OpCodes.Castclass, collectionType);
					GenerateReadField(elementFormalType, false);
					generator.Emit(OpCodes.Callvirt, addMethod);

					if(addMethod.ReturnType != typeof(void))
					{
						generator.Emit(OpCodes.Pop); // remove returned unused value from stack
					}
				});
			}
		}

		private void GenerateReadArray(Type elementFormalType, LocalBuilder objectIdLocal)
		{
			var rankLocal = generator.DeclareLocal(typeof(Int32));
			var lengthsLocal = generator.DeclareLocal(typeof(Int32[]));
			var arrayLocal = generator.DeclareLocal(typeof(Array));
			var positionLocal = generator.DeclareLocal(typeof(Int32[]));
			var loopControlLocal = generator.DeclareLocal(typeof(bool));
			var loopBeginLabel = generator.DefineLabel();
			var loopEndLabel = generator.DefineLabel();
			var lengthIsZeroLabel = generator.DefineLabel();

			var isNotEmptyArrayLocal = generator.DeclareLocal(typeof(bool));
						
			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, rankLocal); // read amount of dimensions of the array
			
			generator.Emit(OpCodes.Ldloc, rankLocal);
			generator.Emit(OpCodes.Newarr, typeof(Int32));
			generator.Emit(OpCodes.Stloc, lengthsLocal); // create an array for keeping the lengths of each dimension

			GenerateLoop(rankLocal, i => {
				generator.Emit(OpCodes.Ldloc, lengthsLocal);
				generator.Emit(OpCodes.Ldloc, i);
				GenerateReadPrimitive(typeof(Int32));
				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Brfalse, lengthIsZeroLabel);

				generator.Emit(OpCodes.Ldc_I4_1);
				generator.Emit(OpCodes.Stloc, isNotEmptyArrayLocal);

				generator.MarkLabel(lengthIsZeroLabel);
				generator.Emit(OpCodes.Stelem, typeof(Int32)); // populate the lengths with values read from stream
			});
			
			PushTypeOntoStack(elementFormalType);
			generator.Emit(OpCodes.Ldloc, lengthsLocal);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Array>(x => Array.CreateInstance(null, new int[0])));
			generator.Emit(OpCodes.Stloc, arrayLocal); // create an multidimensional array
			
			SaveNewDeserializedObject(objectIdLocal, () => {
				generator.Emit(OpCodes.Ldloc, arrayLocal);
			}); // store created array to deserialized obejcts collection

			generator.Emit(OpCodes.Ldloc, rankLocal);
			generator.Emit(OpCodes.Newarr, typeof(Int32));
			generator.Emit(OpCodes.Stloc, positionLocal); // create an array for keeping the current position of each dimension

			generator.Emit(OpCodes.Ldloc, isNotEmptyArrayLocal);
			generator.Emit(OpCodes.Stloc, loopControlLocal); // initialize loop control variable

			generator.MarkLabel(loopBeginLabel);
			generator.Emit(OpCodes.Ldloc, loopControlLocal);
			generator.Emit(OpCodes.Brfalse, loopEndLabel);

			generator.Emit(OpCodes.Ldloc, arrayLocal);
			GenerateReadField(elementFormalType, true);
			generator.Emit(OpCodes.Ldloc, positionLocal);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Array>(a => a.SetValue(null, new int[0])));

			generator.Emit(OpCodes.Ldloc, positionLocal);
			generator.Emit(OpCodes.Ldloc, lengthsLocal);
			generator.Emit(OpCodes.Ldloc, rankLocal);

			GenerateCodeFCall<int[], int[], int, bool>((counter, sizes, ranks) => {
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

			generator.Emit(OpCodes.Stloc, loopControlLocal);

			generator.Emit(OpCodes.Br, loopBeginLabel);
			generator.MarkLabel(loopEndLabel);
		}

		#endregion

		private LocalBuilder GenerateCheckObjectMeta(LocalBuilder objectIdLocal)
		{
			// method returns on stack:
			// 0 - object type read sucessfully and stored in returned local
			// 1 - object has already been deserialized

			var objNotYetDeserialized = generator.DefineLabel();
			var finish = generator.DefineLabel();
			var actualTypeLocal = generator.DeclareLocal(typeof(Type));

			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Call, Helpers.GetPropertyGetterInfo<AutoResizingList<object>, int>(x => x.Count)); // check current length of DeserializedObjectsCollection
			generator.Emit(OpCodes.Ldloc, objectIdLocal);
			generator.Emit(OpCodes.Ble, objNotYetDeserialized); // jump to object creation if objectId > DOC.Count
			generator.Emit(OpCodes.Ldc_I4_1); // push value <<1>> on stack indicating that object has been deserialized earlier
			generator.Emit(OpCodes.Br, finish); // jump to the end

			generator.MarkLabel(objNotYetDeserialized);

			GenerateReadType();
			generator.Emit(OpCodes.Stloc, actualTypeLocal);
			generator.Emit(OpCodes.Ldc_I4_0); // push value <<0>> on stack to indicate that there is actual type next
			
			generator.MarkLabel(finish);

			return actualTypeLocal; // you should use this local only when value on stack is equal to 0; i tried to push two values on stack, but it didn't work
		}

		private void GenerateReadField(Type formalType, bool boxIfValueType = true)
		{
			// method returns read field value on stack

			if(Helpers.CheckTransientNoCache(formalType))
			{
				PushTypeOntoStack(formalType);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, object>(x => Helpers.GetDefaultValue(null)));

				if(formalType.IsValueType && boxIfValueType)
				{
					generator.Emit(OpCodes.Box, formalType);
				}
				return; //return Helpers.GetDefaultValue(_formalType);
			}

			var finishLabel = generator.DefineLabel();
			var else1 = generator.DefineLabel();
			var returnNullLabel = generator.DefineLabel();
			var continueWithNullableLabel = generator.DefineLabel();
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectActualTypeLocal = generator.DeclareLocal(typeof(Type));
			var metaResultLocal = generator.DeclareLocal(typeof(Int32));

			var isBoxed = false;
			var forcedFormalType = formalType;
			var forcedBoxIfValueType = boxIfValueType;

			if(!formalType.IsValueType)
			{
				GenerateReadPrimitive(typeof(Int32)); // read object reference
				generator.Emit(OpCodes.Stloc, objectIdLocal);
				
				generator.Emit(OpCodes.Ldc_I4, AntMicro.Migrant.Consts.NullObjectId);
				generator.Emit(OpCodes.Ldloc, objectIdLocal); 
				generator.Emit(OpCodes.Beq, returnNullLabel); // check if object reference is not <<NULL>>
				
				objectActualTypeLocal = GenerateCheckObjectMeta(objectIdLocal); // read object Type
				generator.Emit(OpCodes.Stloc, metaResultLocal);
				
				generator.Emit(OpCodes.Ldloc, metaResultLocal);
				generator.Emit(OpCodes.Brtrue, else1); // if no actual type on stack jump to the end

				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Ldloc, objectActualTypeLocal);
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader>(r => r.ReadObjectInnerGenerated(typeof(void), 0)));
				
				generator.MarkLabel(else1); // if CheckObjectMeta returned 1
				
				PushDeserializedObjectOntoStack(objectIdLocal);
				generator.Emit(OpCodes.Castclass, formalType);
				generator.Emit(OpCodes.Br, finishLabel);
				
				generator.MarkLabel(returnNullLabel);
				generator.Emit(OpCodes.Ldnull);

				generator.MarkLabel(finishLabel);
				return;
			}
			if(formalType.IsEnum)
			{
				var actualType = Enum.GetUnderlyingType(forcedFormalType);
				
				PushTypeOntoStack(forcedFormalType);
				GenerateReadPrimitive(actualType);
				generator.Emit(OpCodes.Call, typeof(Enum).GetMethod("ToObject", BindingFlags.Static | BindingFlags.Public, null, new[] {
					typeof(Type),
					actualType
				}, null));
				
				if(!forcedBoxIfValueType)
				{
					generator.Emit(OpCodes.Unbox_Any, forcedFormalType);
				}
				return;
			}

			var nullableActualType = Nullable.GetUnderlyingType(formalType);
			if(nullableActualType != null)
			{
				forcedFormalType = nullableActualType;
				forcedBoxIfValueType = true;

				GenerateReadPrimitive(typeof(bool));
				generator.Emit(OpCodes.Brtrue, continueWithNullableLabel);

				generator.Emit(OpCodes.Ldnull);
				generator.Emit(OpCodes.Br, finishLabel);

				generator.MarkLabel(continueWithNullableLabel);
			}

			if(Helpers.IsWriteableByPrimitiveWriter(forcedFormalType))
			{
				// value type
				GenerateReadPrimitive(forcedFormalType);

				if(forcedBoxIfValueType)
				{
					generator.Emit(OpCodes.Box, forcedFormalType);
					isBoxed = true;
				}
			}
			else
			{
				// here we have struct
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Ldtoken, formalType);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<RuntimeTypeHandle, Type>(o => Type.GetTypeFromHandle(o)));
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader, Type>((reader, type) => reader.ReadStamp(type)));
				var structLocal = generator.DeclareLocal(forcedFormalType);
				GenerateUpdateStructFields(forcedFormalType, structLocal);

				generator.Emit(OpCodes.Ldloc, structLocal);
				if(forcedBoxIfValueType)
				{
					generator.Emit(OpCodes.Box, forcedFormalType);
					isBoxed = true;
				}
			}

			generator.MarkLabel(finishLabel);

			// if the value is nullable we must use special initialization of it
			if(nullableActualType != null)
			{
				var nullableLocal = generator.DeclareLocal(formalType);
				var returnNullNullableLabel = generator.DefineLabel();
				var endLabel = generator.DefineLabel();

				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Brfalse, returnNullNullableLabel);

				if(isBoxed)
				{
					generator.Emit(OpCodes.Unbox_Any, nullableActualType);
				}
				generator.Emit(OpCodes.Newobj, formalType.GetConstructor(new [] { nullableActualType }));
				generator.Emit(OpCodes.Stloc, nullableLocal);
				generator.Emit(OpCodes.Ldloc, nullableLocal);

				if(boxIfValueType)
				{
					generator.Emit(OpCodes.Box, formalType);
				}

				generator.Emit(OpCodes.Br, endLabel);

				generator.MarkLabel(returnNullNullableLabel);
				generator.Emit(OpCodes.Pop);
				generator.Emit(OpCodes.Ldloca, nullableLocal);
				generator.Emit(OpCodes.Initobj, formalType);
				
				generator.Emit(OpCodes.Ldloc, nullableLocal);
				if(boxIfValueType)
				{
					generator.Emit(OpCodes.Box, nullableLocal);
				}

				generator.MarkLabel(endLabel);
			}
		}

		private void GenerateUpdateFields(Type formalType, LocalBuilder objectIdLocal)
		{
			var fields = typeStamper.GetFieldsToDeserialize(formalType);
			foreach(var field in fields)
			{
				if(field.IsDefined(typeof(TransientAttribute), false))
				{
					if(field.IsDefined(typeof(ConstructorAttribute), false))
					{
						generator.Emit(OpCodes.Ldtoken, field);
						if (field.DeclaringType.IsGenericType)
						{
                            generator.Emit(OpCodes.Ldtoken, field.ReflectedType);
                            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, object>(x => FieldInfo.GetFieldFromHandle(field.FieldHandle, new RuntimeTypeHandle())));
						}
						else
						{
                            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, object>(x => FieldInfo.GetFieldFromHandle(field.FieldHandle)));
						}
						PushDeserializedObjectOntoStack(objectIdLocal);

						GenerateCodeCall<FieldInfo, object>((fi, target) => {
							var ctorAttribute = (ConstructorAttribute)fi.GetCustomAttributes(false).First(x => x is ConstructorAttribute);
							fi.SetValue(target, Impromptu.InvokeConstructor(fi.FieldType, ctorAttribute.Parameters));
						});

					}
					continue;
				}

                PushDeserializedObjectOntoStack(objectIdLocal);
                generator.Emit(OpCodes.Castclass, field.ReflectedType);
                GenerateReadField(field.FieldType, false);
                generator.Emit(OpCodes.Stfld, field);
			}
		}

		private void GenerateUpdateStructFields(Type formalType, LocalBuilder structLocal)
		{			
			var fields = TypeStamper.GetFieldsInSerializationOrder(formalType).ToList();
			foreach(var field in fields)
			{
				generator.Emit(OpCodes.Ldloca, structLocal);
				GenerateReadField(field.FieldType, false);
				generator.Emit(OpCodes.Stfld, field);
			}
		}

		#region Push onto stack methods and other helper methods

		private void PushPrimitiveReaderOntoStack()
		{
			generator.Emit(OpCodes.Ldarg_0); // object reader
			generator.Emit(OpCodes.Ldfld, Helpers.GetFieldInfo<ObjectReader, PrimitiveReader>(x => x.reader));
		}

		private void PushDeserializedObjectsCollectionOntoStack()
		{
			generator.Emit(OpCodes.Ldarg_0); // object reader	
			generator.Emit(OpCodes.Ldfld, Helpers.GetFieldInfo<ObjectReader, AutoResizingList<object>>(x => x.deserializedObjects));
		}

		private void PushDeserializedObjectOntoStack(LocalBuilder local)
		{
			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Ldloc, local);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0])); // pushes reference to an object to update (id is wrong due to structs)
		}

		private void SaveNewDeserializedObject(LocalBuilder objectIdLocal, Action generateNewObject)
		{
			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Ldloc, objectIdLocal);

			generateNewObject();

			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
		}

		private void PushTypeOntoStack(Type type)
		{
			generator.Emit(OpCodes.Ldtoken, type);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<RuntimeTypeHandle, Type>(o => Type.GetTypeFromHandle(o))); // loads value of <<typeToGenerate>> onto stack
		}

		private void GenerateCodeCall(Action a)
		{
			generator.Emit(OpCodes.Call, a.Method);
		}
		
		private void GenerateCodeCall<T>(Action<T> a)
		{
			generator.Emit(OpCodes.Call, a.Method);
		}
		
		private void GenerateCodeCall<T1, T2>(Action<T1, T2> a)
		{
			generator.Emit(OpCodes.Call, a.Method);
		}

		private void GenerateCodeCall<T1, T2, T3>(Action<T1, T2, T3> a)
		{
			generator.Emit(OpCodes.Call, a.Method);
		}

		private void GenerateCodeCall<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> a)
		{
			generator.Emit(OpCodes.Call, a.Method);
		}

		private void GenerateCodeFCall<TResult, T1, T2, T3>(Func<TResult, T1, T2, T3> f)
		{
			generator.Emit(OpCodes.Call, f.Method);
		}

		#endregion

		private void GenerateTouchObject(Type formalType)
		{
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectTypeLocal = generator.DeclareLocal(typeof(Type));

			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Stloc, objectIdLocal);

			PushTypeOntoStack(formalType);
			generator.Emit(OpCodes.Stloc, objectTypeLocal);

			switch(ObjectReader.GetCreationWay(formalType))
			{
			case ObjectReader.CreationWay.Null:
				break;
			case ObjectReader.CreationWay.DefaultCtor:
                    // execute if <<localId>> was not found in DOC
				PushDeserializedObjectsCollectionOntoStack();
					
				generator.Emit(OpCodes.Ldloc, objectIdLocal); // first argument
				generator.Emit(OpCodes.Ldloc, objectTypeLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Activator.CreateInstance(typeof(void)))); // second argument
					
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, new object())));
				break;
			case ObjectReader.CreationWay.Uninitialized:
                    // execute if <<localId>> was not found in DOC
				PushDeserializedObjectsCollectionOntoStack();
					
				generator.Emit(OpCodes.Ldloc, objectIdLocal); // first argument
                    //PushTypeOntoStack(formalType);
				generator.Emit(OpCodes.Ldloc, objectTypeLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => FormatterServices.GetUninitializedObject(typeof(void)))); // second argument
					
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, new object())));
				break;
			}
		}

		private void GenerateReadType()
		{
			// method returns read type (or null) 

			generator.Emit(OpCodes.Ldarg_0); // object reader
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader, Type>(or => or.ReadType()));
		}

		public DynamicMethod Method
		{
			get
			{ 
				return dynamicMethod; 
			}
		}

		private ILGenerator generator;
		private readonly DynamicMethod dynamicMethod;
		private readonly TypeStamper typeStamper;
		private const string InternalErrorMessage = "Internal error: should not reach here.";
		private const string CouldNotFindAddErrorMessage = "Could not find suitable Add method for the type {0}.";
		private static readonly Type[] ParameterTypes = new [] {
			typeof(ObjectReader),
			typeof(Int32)
		};
	}
}
