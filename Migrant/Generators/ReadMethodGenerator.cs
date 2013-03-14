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
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Text;
using AntMicro.Migrant;
using AntMicro.Migrant.Utilities;
using System.Collections;
using System.Linq.Expressions;

namespace Migrant.Generators
{
    public class ReadMethodGenerator
    {
		public ReadMethodGenerator(Type typeToGenerate)
		{
		    dynamicMethod = new DynamicMethod("Read", typeof(object), ParameterTypes, true);
		    generator = dynamicMethod.GetILGenerator();

			GenerateDynamicCode(typeToGenerate);
			SaveToFile(typeToGenerate);

			//Temp(typeToGenerate);
		}

		private void Temp(Type t)
		{
			var str = generator.DeclareLocal(t);
			generator.Emit(OpCodes.Ldloca, str);
			generator.Emit(OpCodes.Initobj, t);
			generator.Emit(OpCodes.Ldloc, str);
			generator.Emit(OpCodes.Box, t);
			//PushTypeOntoStack(t);
			//generator.Emit(OpCodes.Newobj, t.GetConstructor(new Type[0]));
			generator.Emit(OpCodes.Ret);


			//	IL_0001: ldloca.s 0
			//	IL_0003: initobj Tests.Structt
			//	IL_0009: ldloc.0
			//	IL_000a: stloc.1
			//	IL_000b: ldloca.s 1
			//	IL_000d: ldc.i4.s 23
			//	IL_000f: stfld int32 Tests.Structt::a
			//	IL_0014: ldloc.1
			//	IL_0015: stloc.0
			//	IL_0016: ldloc.0
			//	IL_0017: box Tests.Structt
			//	IL_001c: stloc.2
			//	
			//	IL_0022: ldloc.2
			//	IL_0023: ret
		}

		public void GenerateInitializationCode()
		{
		    //REFERENCES_LOCAL_ID = generator.DeclareLocal(typeof(int)).LocalIndex;
			//DESERIALIZED_OBJECTS_LOCAL_ID = generator.DeclareLocal(typeof(AutoResizingList<object>)).LocalIndex;
			//NEXT_OBJECT_TO_READ_LOACL_ID = generator.DeclareLocal(typeof(int)).LocalIndex;
			//INLINE_READ_LOCAL_ID = generator.DeclareLocal(typeof(HashSet<int>)).LocalIndex;

			//generator.Emit(OpCodes.Ldc_I4, 4);
			//generator.Emit(OpCodes.Newobj, typeof(AutoResizingList<object>).GetConstructor(new[] {typeof(int)}) );
			//generator.Emit(OpCodes.Stloc, DESERIALIZED_OBJECTS_LOCAL_ID);

			//generator.Emit(OpCodes.Newobj, typeof(HashSet<int>).GetConstructor(Type.EmptyTypes));
			//generator.Emit(OpCodes.Stloc, INLINE_READ_LOCAL_ID);

			//generator.Emit(OpCodes.Ldc_I4_0);
			//generator.Emit(OpCodes.Stloc, REFERENCES_LOCAL_ID);

			//generator.Emit(OpCodes.Ldc_I4_0);
			//generator.Emit(OpCodes.Stloc, NEXT_OBJECT_TO_READ_LOACL_ID);
		}

        public void GenerateDynamicCode(Type typeToGenerate)
		{
			GenerateReadObjectInner(typeToGenerate, false); // TODO: moim skromnym zdaniem to cuś powoduje sporo problemów! (chodzi o parametr false)
			
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

		private void GenerateReadNotPrecreated(Type formalType)
		{
			// method reads int from stack
			// objId - identifier of an object reference

			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			generator.Emit(OpCodes.Stloc, objectIdLocal);

			if (formalType.IsValueType)
			{
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				GenerateReadField(formalType, true);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
			}
			else if (formalType == typeof(string))
			{
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit (OpCodes.Ldloc, objectIdLocal);
				GenerateReadPrimitive(formalType);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));
			}
			else if (formalType.IsArray)
			{
				/*
				generator.Emit(OpCodes.Ldarg_0);
				PushTypeOntoStack(formalType.GetElementType());
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader>(x => x.ReadArray(null, 0)));*/

				GenerateReadArray(formalType.GetElementType(), objectIdLocal);
			}
			else if (typeof(MulticastDelegate).IsAssignableFrom(formalType))
			{
				GenereateReadDelegate(formalType, objectIdLocal);
			}
			else if (formalType.IsGenericType && typeof(ReadOnlyCollection<>).IsAssignableFrom(formalType.GetGenericTypeDefinition()))
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
			var methodLocal = generator.DeclareLocal(typeof(MethodInfo));
			var delLocal = generator.DeclareLocal(typeof(Delegate));

			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, invocationListLengthLocal);

			GenerateLoop(invocationListLengthLocal, cl => {
				GenerateReadField(typeof(object));
				generator.Emit(OpCodes.Stloc, targetLocal);

				GenerateReadType();
				generator.Emit(OpCodes.Stloc, containingTypeLocal);

				GenerateReadPrimitive(typeof(Int32));
				generator.Emit(OpCodes.Stloc, methodNumberLocal);

				/*
				generator.Emit(OpCodes.Ldloc, containingTypeLocal);
				generator.Emit(OpCodes.Ldfld, Helpers.GetPropertyGetterInfo<Type, Module>(t => t.Module));
				generator.Emit(OpCodes.Ldloc, methodNumberLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Module, MethodBase>(m => m.ResolveMethod(0)));
				generator.Emit(OpCodes.Castclass, typeof(MethodInfo));
				generator.Emit(OpCodes.Stloc, methodLocal);*/


				generator.Emit(OpCodes.Ldloc, containingTypeLocal);
				generator.Emit(OpCodes.Ldloc, methodNumberLocal);
				PushTypeOntoStack(type);
				generator.Emit(OpCodes.Ldloc, targetLocal);
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => ReadMethodGenerator.ReadDelegateHelper(null, 0, null, null, null, 0)));

				/*
				PushTypeOntoStack(type);
				generator.Emit(OpCodes.Ldloc, targetLocal);
				generator.Emit(OpCodes.Ldloc, methodLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Delegate.CreateDelegate(null, null, null)));
				generator.Emit(OpCodes.Stloc, delLocal);

				SaveNewDeserializedObject(objectIdLocal, () => {
					PushDeserializedObjectOntoStack(objectIdLocal);
					generator.Emit(OpCodes.Castclass, typeof(Delegate));
					generator.Emit(OpCodes.Ldloc, delLocal);
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Delegate.Combine(null, null)));
				});*/
			});
		}

		private static void ReadDelegateHelper(Type containingType, int methodNumber, Type type, object target, AutoResizingList<object> deserializedObjects, int objectId)
		{
			// constructor cannot be bound to delegate, so we can just cast to methodInfo
			var method = (MethodInfo)containingType.Module.ResolveMethod(methodNumber);
			var del = Delegate.CreateDelegate(type, target, method);
			deserializedObjects[objectId] = Delegate.Combine((Delegate)deserializedObjects[objectId], del);
		}

		private void GenerateReadObjectInner(Type formalType, bool generateReadType = true)
		{
			// method reads one argument from stack iff generateReadType is set to true
			// objId - identifier of object to create

		    var finish = generator.DefineLabel();
		    var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			//var objectTypeLocal = generator.DeclareLocal(typeof(Type));

			if (generateReadType)
			{
				generator.Emit(OpCodes.Stloc, objectIdLocal);
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
			}
			else
			{
				generator.Emit(OpCodes.Ldarg_1);
				generator.Emit(OpCodes.Stloc, objectIdLocal);
			}

			GenerateTouchObject(formalType, generateReadType);
			generator.Emit(OpCodes.Brtrue, finish); // if object has already been created skip initialization

			switch (ObjectReader.GetCreationWay(formalType))
		    {
                case ObjectReader.CreationWay.Null:
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
					GenerateReadNotPrecreated(formalType);
                    break;
                case ObjectReader.CreationWay.DefaultCtor:
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
					GenerateUpdateElements(formalType);
                    break;
                case ObjectReader.CreationWay.Uninitialized:
					PushDeserializedObjectOntoStack(objectIdLocal);
				    GenerateUpdateFields(formalType);
                    break;
            }

			generator.MarkLabel(finish);
		}

		private static void DeserializeSpeciallySerializableObject(ISpeciallySerializable obj, PrimitiveReader reader)
		{
			var beforePosition = reader.Position;
			obj.Load(reader);
			var afterPosition = reader.Position;
			var serializedLength = reader.ReadInt64();
			if(serializedLength + beforePosition != afterPosition)
			{
				throw new InvalidOperationException(string.Format(
					"Stream corruption by '{0}', {1} bytes was read.", obj, serializedLength));
			}
			return;
		}

		private void GenerateUpdateElements(Type formalType)
		{
			// method reads one value from stack
			// objId - identifier of an object to update

			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			generator.Emit(OpCodes.Stloc, objectIdLocal);

			if (typeof(ISpeciallySerializable).IsAssignableFrom(formalType))
			{
				PushDeserializedObjectOntoStack(objectIdLocal);
				generator.Emit(OpCodes.Castclass, typeof(ISpeciallySerializable));
				PushPrimitiveReaderOntoStack();
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => ReadMethodGenerator.DeserializeSpeciallySerializableObject(null, null)));
				return;
			}
			Type formalKeyType, formalValueType;
			bool isGenericDictionary;
			if (Helpers.IsDictionary(formalType, out formalKeyType, out formalValueType, out isGenericDictionary))
			{
				PushDeserializedObjectOntoStack(objectIdLocal);
				GenerateFillDictionary(formalKeyType, formalValueType, formalType);
				return;
			}
			Type elementFormalType;
			bool fake, fake2, fake3;
			if (!Helpers.IsCollection(formalType, out elementFormalType, out fake, out fake2, out fake3))
			{
				throw new InvalidOperationException(InternalErrorMessage);
			}
			// TODO: jak dla mnie to ten special case nie jest potrzebny, gdyż funkcjonalność ta jest pokryta przez FillCollection
			/*if (typeof(IList).IsAssignableFrom(formalValueType))
			{
				PushDeserializedObjectOntoStack(objectIdLocal);
				GenerateFillList(elementFormalType, formalType);
				return;
			}*/

			PushDeserializedObjectOntoStack(objectIdLocal);
			GenerateFillCollection(elementFormalType, formalType);
		}

		private void GenerateLoop(LocalBuilder countLocal, Action<LocalBuilder> loopAction, bool reversed = false)
		{
			var loopControlLocal = generator.DeclareLocal(typeof(Int32));
			
			var loopLabel = generator.DefineLabel();
			var loopFinishLabel = generator.DefineLabel();

			if (reversed)
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

			if (reversed)
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

		#region Collection helpers

		private void GenerateFillList(Type elementFormalType, Type listType)
		{
			// method read one value from stack
			// objRef - reference to the list object

			var listObjLocal = generator.DeclareLocal(listType);
			var countLocal = generator.DeclareLocal(typeof(Int32));

			generator.Emit(OpCodes.Stloc, listObjLocal);

			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, countLocal); // read list elements count

			GenerateLoop(countLocal, cl => {
				generator.Emit(OpCodes.Ldloc, listObjLocal);
				GenerateReadField(elementFormalType, false);
				generator.Emit(OpCodes.Call, listType.GetMethod("Add", new[] { elementFormalType }));
				generator.Emit(OpCodes.Pop); // Add method returns integer thath should be removed from stack
			});
		}

		private void GenerateFillDictionary(Type formalKeyType, Type formalValueType, Type dictionaryType)
		{
			// method read one value from stack
			// objRef - reference to the dictionary object

			var dicObjLocal = generator.DeclareLocal(dictionaryType);
			var countLocal = generator.DeclareLocal(typeof(Int32));

			generator.Emit(OpCodes.Stloc, dicObjLocal);

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
				// TODO: it might not work with structs, however they should be boxed - to check!
				generator.Emit(OpCodes.Ldloc, dicObjLocal);
				GenerateReadField(formalKeyType, false);
				GenerateReadField(formalValueType, false);
				generator.Emit(OpCodes.Call, addMethod);
				if (addMethod.ReturnType != typeof(void))
				{
					generator.Emit(OpCodes.Pop); // remove returned unused value from stack
				}
			});
		}
				
		private void GenerateReadReadOnlyCollection(Type type, LocalBuilder objectIdLocal)
		{
			var elementFormalType = type.GetGenericArguments()[0];
			
			var lengthLocal = generator.DeclareLocal(typeof(Int32));
			var arrayLocal = generator.DeclareLocal(typeof(Array));
			
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
				generator.Emit(OpCodes.Newobj, type.GetConstructor(new [] { typeof(IList<>).MakeGenericType(elementFormalType) }));
			});
			
		}
				
		private void GenerateFillCollection(Type elementFormalType, Type collectionType)
		{
			// method read one value from stack
			// objRef - reference to the collection object
			
			var collectionObjLocal = generator.DeclareLocal(collectionType);
			var countLocal = generator.DeclareLocal(typeof(Int32));
			
			generator.Emit(OpCodes.Stloc, collectionObjLocal);
			
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
					generator.Emit(OpCodes.Ldloc, collectionObjLocal);
					generator.Emit(OpCodes.Ldloc, tempArrLocal);
					generator.Emit(OpCodes.Ldloc, cl);
					generator.Emit(OpCodes.Ldelem, elementFormalType);
					generator.Emit(OpCodes.Call, collectionType.GetMethod("Push"));
				}, true);
			}
			else
			{
				GenerateLoop(countLocal, cl => {
					generator.Emit(OpCodes.Ldloc, collectionObjLocal);
					GenerateReadField(elementFormalType, false);
					generator.Emit(OpCodes.Call, addMethod);
					if (addMethod.ReturnType != typeof(void))
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
			
			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, rankLocal); // read amount of dimensions of the array
			
			generator.Emit(OpCodes.Ldloc, rankLocal);
			generator.Emit(OpCodes.Newarr, typeof(Int32));
			generator.Emit(OpCodes.Stloc, lengthsLocal); // create an array for keeping the lengths of each dimension
			
			GenerateLoop(rankLocal, i => {
				generator.Emit(OpCodes.Ldloc, lengthsLocal);
				generator.Emit(OpCodes.Ldloc, i);
				GenerateReadPrimitive(typeof(Int32));
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
			
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldloc, arrayLocal);
			generator.Emit(OpCodes.Ldc_I4_0);
			generator.Emit(OpCodes.Ldloc, positionLocal);
			PushTypeOntoStack(elementFormalType);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader>(x => x.FillArrayRowRecursive(null, 0, null, null))); // TODO: tu trzeba przepisać!!!
		}

		//private static void ReadArrayHelper()
		//{

		//}

		#endregion

		private LocalBuilder GenerateCheckObjectMeta()
		{
			// method read one value from stack
			// objectId - identifier of an object

			// method returns on stack:
			// 0 - object type read sucessfully and stored in returned local
			// 1 - object has already been deserialized

			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objNotYetDeserialized = generator.DefineLabel();
			var finish = generator.DefineLabel();
			var createObj = generator.DefineLabel();
			var actualTypeLocal = generator.DeclareLocal(typeof(Type));

			generator.Emit(OpCodes.Stloc, objectIdLocal);

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

		#region DEBUG

		private void DEBUG_CALL(string text)
		{
			generator.Emit(OpCodes.Ldstr, text);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Helpers.DEBUG_BREAKPOINT_FUNC("text")));
		}

		private void DEBUG_STACKVALUE<T>(string text)
		{
			generator.Emit(OpCodes.Dup);

			if (typeof(T).IsValueType)
			{
				generator.Emit(OpCodes.Box, typeof(T));
			}
			generator.Emit(OpCodes.Ldstr, text);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Helpers.DEBUG_BREAKPOINT_FUNC(null, "text")));
		}

		private void DEBUG_LOCALVALUE(string text, LocalBuilder local)
		{
			generator.Emit(OpCodes.Ldloc, local);
			if (local.LocalType.IsValueType)
			{
				generator.Emit(OpCodes.Box, local.LocalType);
			}

			generator.Emit(OpCodes.Ldstr, text);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Helpers.DEBUG_BREAKPOINT_FUNC(null, "text")));
		}

		#endregion

		private void GenerateReadField(Type _formalType, bool _boxIfValueType = true)
		{
			// method returns read field value on stack

		    var finishLabel = generator.DefineLabel();
		    var updateMaximumReferenceIdLabel = generator.DefineLabel();
			var else1 = generator.DefineLabel();
			var returnNullLabel = generator.DefineLabel();
			var continueWithNullableLabel = generator.DefineLabel();
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectActualTypeLocal = generator.DeclareLocal(typeof(Type));
			var metaResultLocal = generator.DeclareLocal(typeof(Int32));

			var isBoxed = false;
			var forcedFormalType = _formalType;
			var forcedBoxIfValueType = _boxIfValueType;

			var nullableActualType = Nullable.GetUnderlyingType(_formalType);
			if (nullableActualType != null)
			{
				forcedFormalType = nullableActualType;
				forcedBoxIfValueType = true;

				GenerateReadPrimitive(typeof(bool));
				generator.Emit(OpCodes.Brtrue, continueWithNullableLabel);

				generator.Emit(OpCodes.Ldnull);
				generator.Emit(OpCodes.Br, finishLabel);

				generator.MarkLabel(continueWithNullableLabel);
			}

			if (PrimitiveTypes.Contains(forcedFormalType))
			{
				// value type
				GenerateReadPrimitive(forcedFormalType);

				if (forcedBoxIfValueType)
				{
					generator.Emit(OpCodes.Box, forcedFormalType);
					isBoxed = true;
				}

				generator.Emit(OpCodes.Br, finishLabel);
			}
			else if (forcedFormalType.IsEnum)
			{
				var actualType = Enum.GetUnderlyingType(forcedFormalType);
								
				PushTypeOntoStack(forcedFormalType);
				GenerateReadPrimitive(actualType);
				generator.Emit(OpCodes.Call, typeof(Enum).GetMethod("ToObject", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Type), actualType }, null));
				isBoxed = true;

				if (!forcedBoxIfValueType)
				{
					generator.Emit(OpCodes.Unbox_Any, forcedFormalType);
					isBoxed = false;
				}

				generator.Emit(OpCodes.Br, finishLabel);
			}
			else if (forcedFormalType.IsValueType)
			{
				// here we have struct

				var structLocal = generator.DeclareLocal(forcedFormalType);
				generator.Emit(OpCodes.Ldloca, structLocal);
				generator.Emit(OpCodes.Initobj, forcedFormalType);
				GenerateUpdateStructFields(forcedFormalType, structLocal);

				generator.Emit(OpCodes.Ldloc, structLocal);
				if (forcedBoxIfValueType)
				{
					generator.Emit(OpCodes.Box, forcedFormalType);
					isBoxed = true;
				}

				generator.Emit(OpCodes.Br, finishLabel);
			}
			else
            {
				GenerateReadPrimitive(typeof(Int32)); // read object reference
				generator.Emit(OpCodes.Stloc, objectIdLocal);

				generator.Emit(OpCodes.Ldc_I4, AntMicro.Migrant.Consts.NullObjectId);
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Beq, returnNullLabel);

				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				objectActualTypeLocal = GenerateCheckObjectMeta();
				generator.Emit(OpCodes.Stloc, metaResultLocal);

				generator.Emit(OpCodes.Ldloc, metaResultLocal);
				generator.Emit(OpCodes.Brtrue, else1); // if no actual type on stack jump to the end

				if (forcedFormalType == typeof(string))
				{
					// special case

					var valueLocal = generator.DeclareLocal(typeof(string));

					GenerateReadPrimitive(typeof(string));
					generator.Emit(OpCodes.Stloc, valueLocal);

					PushDeserializedObjectsCollectionOntoStack();
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
					generator.Emit(OpCodes.Ldloc, valueLocal);
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, null)));

					generator.Emit(OpCodes.Ldloc, valueLocal);
					generator.Emit(OpCodes.Br, finishLabel);
				}
				else
				{
					generator.Emit(OpCodes.Ldarg_0);
					generator.Emit(OpCodes.Ldloc, objectActualTypeLocal);
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader>(r => r.ReadObjectInner2(typeof(void), 0)));
				}

				generator.MarkLabel(else1); // if CheckObjectMeta returned 1

				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0]));
				generator.Emit(OpCodes.Br, finishLabel);

				generator.MarkLabel(returnNullLabel);
				generator.Emit(OpCodes.Ldnull);
		    }

			generator.MarkLabel(finishLabel);

			// if the value is nullable we must use special initialization of it
			if (nullableActualType != null)
			{
				var nullableLocal = generator.DeclareLocal(_formalType);
				var returnNullNullableLabel = generator.DefineLabel();
				var endLabel = generator.DefineLabel();

				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Brfalse, returnNullNullableLabel);

				if (isBoxed)
				{
					generator.Emit(OpCodes.Unbox_Any, nullableActualType);
				}
				generator.Emit(OpCodes.Newobj, _formalType.GetConstructor(new [] { nullableActualType }));
				generator.Emit(OpCodes.Stloc, nullableLocal);
				generator.Emit(OpCodes.Ldloc, nullableLocal);

				if (_boxIfValueType)
				{
					generator.Emit(OpCodes.Box, _formalType);
				}

				generator.Emit(OpCodes.Br, endLabel);

				generator.MarkLabel(returnNullNullableLabel);
				generator.Emit(OpCodes.Pop);
				generator.Emit(OpCodes.Ldloca, nullableLocal);
				generator.Emit(OpCodes.Initobj, _formalType);
				
				generator.Emit(OpCodes.Ldloc, nullableLocal);
				if (_boxIfValueType)
				{
					generator.Emit(OpCodes.Box, nullableLocal);
				}

				generator.MarkLabel(endLabel);
			}
		}

		private void GenerateUpdateFields(Type formalType)
		{
			// method reads one argument from stack
			// obj - reference to an object to update

			var objectLocal = generator.DeclareLocal(formalType);
			generator.Emit(OpCodes.Stloc, objectLocal);

		    var fields = ObjectReader.GetFieldsToSerialize(formalType).ToList();
		    foreach (var field in fields)
		    {
				var tmpLocal = generator.DeclareLocal(field.FieldType);
				GenerateReadField(field.FieldType, false);
				generator.Emit(OpCodes.Stloc, tmpLocal);
				
				generator.Emit(OpCodes.Ldloc, objectLocal);
				generator.Emit(OpCodes.Ldloc, tmpLocal);
				generator.Emit(OpCodes.Stfld, field);
		    }
		}

		private void GenerateUpdateStructFields(Type formalType, LocalBuilder structLocal)
		{			
			var fields = ObjectReader.GetFieldsToSerialize(formalType).ToList();
			foreach (var field in fields)
			{
				var tmpLocal = generator.DeclareLocal(field.FieldType);
				GenerateReadField(field.FieldType, false);
				generator.Emit(OpCodes.Stloc, tmpLocal);

				generator.Emit(OpCodes.Ldloca, structLocal);
				generator.Emit(OpCodes.Ldloc, tmpLocal);
				generator.Emit(OpCodes.Stfld, field);
			}
		}

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

		private void GenerateTouchObject(Type formalType, bool generateReadType = true)
		{
			// method reads one value from stack iff generateReadType is set to true:
			// objId - identifier of an object reference to touch

			// method leaves int value onto stack:
			// 2 - the reference to the object is null
			// 1 - the object has already been deserialized and shouldn't be recreated again
			// 0 - the object has been just created and should be properely initialized

		    var finish = generator.DefineLabel();
			var objNotYetDeserialized = generator.DefineLabel();
			var createObj = generator.DefineLabel();
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectTypeLocal = generator.DeclareLocal(typeof(Type));

			if (generateReadType)
			{
				generator.Emit(OpCodes.Stloc, objectIdLocal);

				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Call, Helpers.GetPropertyGetterInfo<AutoResizingList<object>, int>(x => x.Count)); // check current length of DeserializedObjectsCollection
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Ble, objNotYetDeserialized); // jump to object creation if objectId > DOC.Count
				generator.Emit(OpCodes.Ldc_I4_1); // push value <<1>> onto stack indicating that object has been deserialized earlier
				generator.Emit(OpCodes.Br, finish); // jump to the end

				generator.MarkLabel(objNotYetDeserialized);

				GenerateReadType();
				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Brtrue, createObj);
				generator.Emit(OpCodes.Pop);
				generator.Emit(OpCodes.Ldc_I4_1); // push value <<2>> onto stack indicating that there is null object
				generator.Emit(OpCodes.Br, finish); // jump to the end

				generator.MarkLabel(createObj);
				generator.Emit(OpCodes.Stloc, objectTypeLocal);
			}
			else
			{
				generator.Emit(OpCodes.Ldarg_1);
				generator.Emit(OpCodes.Stloc, objectIdLocal);

				PushTypeOntoStack(formalType);
				generator.Emit(OpCodes.Stloc, objectTypeLocal);
			}

		    switch (ObjectReader.GetCreationWay(formalType))
		    {
                case ObjectReader.CreationWay.Null:
                    break;
                case ObjectReader.CreationWay.DefaultCtor:
                    // execute if <<localId>> was not found in DOC
					PushDeserializedObjectsCollectionOntoStack();
					
					generator.Emit(OpCodes.Ldloc, objectIdLocal); // first argument
					//PushTypeOntoStack(formalType);
					generator.Emit(OpCodes.Ldloc, objectTypeLocal);
                    generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Activator.CreateInstance(typeof(void)))); // second argument
					
                    generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x =>  x.SetItem(0, new object())));
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

			generator.Emit(OpCodes.Ldc_I4_0); // push value <<0>> onto stack to indicate that object has just been created and needs initialization
		    generator.MarkLabel(finish);
		}

		/*

		private void GenerateReadArray(Type elementFormalType, LocalBuilder objectIdLocal)
		{
			var rankLocal = generator.DeclareLocal(typeof(Int32));
			var lengthsLocal = generator.DeclareLocal(typeof(Int32[]));
			var arrayLocal = generator.DeclareLocal(typeof(Array));
			var positionLocal = generator.DeclareLocal(typeof(Int32[]));

			GenerateReadPrimitive(typeof(Int32));
			generator.Emit(OpCodes.Stloc, rankLocal); // read amount of dimensions of the array

			generator.Emit(OpCodes.Ldloc, rankLocal);
			generator.Emit(OpCodes.Newarr, typeof(Int32));
			generator.Emit(OpCodes.Stloc, lengthsLocal); // create an array for keeping the lengths of each dimension

			GenerateLoop(rankLocal, i => {
				generator.Emit(OpCodes.Ldloc, lengthsLocal);
				generator.Emit(OpCodes.Ldloc, i);
				GenerateReadPrimitive(typeof(Int32));
				generator.Emit(OpCodes.Stelem, typeof(Int32)); // populate the lengths with values read from stream
			});

			PushTypeOntoStack(elementFormalType);
			generator.Emit(OpCodes.Ldloc, lengthsLocal);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Array>(x => Array.CreateInstance(null, new int[0])));
			generator.Emit(OpCodes.Stloc, arrayLocal); // create an multidimensional array

			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Ldloc, objectIdLocal); 
			generator.Emit(OpCodes.Ldloc, arrayLocal);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, new object()))); // store created array to deserialized obejcts collection

			generator.Emit(OpCodes.Ldloc, rankLocal);
			generator.Emit(OpCodes.Newarr, typeof(Int32));
			generator.Emit(OpCodes.Stloc, positionLocal); // create an array for keeping the current position of each dimension

			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldloc, arrayLocal);
			generator.Emit(OpCodes.Ldc_I4_0);
			generator.Emit(OpCodes.Ldloc, positionLocal);
			PushTypeOntoStack(elementFormalType);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<ObjectReader>(x => x.FillArrayRowRecursive(null, 0, null, null)));
		}


		private void GenerateUpdateMaximumReferenceId()
		{


			if(localId.HasValue)
			{
				generator.Emit(OpCodes.Ldloc, OBJECTS_CREATED_LOCAL_ID);
				generator.Emit(OpCodes.Ldc_I4, localId.Value);
			}
			else
			{
			    var l = generator.DeclareLocal(typeof(int));
				generator.Emit(OpCodes.Stloc, l);
				generator.Emit(OpCodes.Ldloc, OBJECTS_CREATED_LOCAL_ID);
				generator.Emit(OpCodes.Ldloc, l);
			}

			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => Math.Max(0, 0)));
			generator.Emit(OpCodes.Stloc, OBJECTS_CREATED_LOCAL_ID);
		}
		*/

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

        public void SaveToFile(Type ttg)
        {
            var da = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("dyn-" + ttg.Name.Substring(0, 3)), // call it whatever you want
                AssemblyBuilderAccess.Save);

            var dm = da.DefineDynamicModule("dyn_mod", "dyn-" + ttg.Name.Substring(0, 3) + ".dll");
            var dt = dm.DefineType("dyn_type", TypeAttributes.Public);

            var method = dt.DefineMethod("Foo", MethodAttributes.Public | MethodAttributes.Static, typeof(object), ParameterTypes);

            generator = method.GetILGenerator();
			GenerateDynamicCode(ttg);

            dt.CreateType();
			
            da.Save("dyn-" + ttg.Name.Substring(0, 3) + ".dll");
        }
		
		private ILGenerator generator;
		private DynamicMethod dynamicMethod;

        //private int REFERENCES_LOCAL_ID;
        //private int OBJECTS_CREATED_LOCAL_ID;
        //private int DESERIALIZED_OBJECTS_LOCAL_ID;
        //private int NEXT_OBJECT_TO_READ_LOACL_ID;
        //private int INLINE_READ_LOCAL_ID;

		private const string InternalErrorMessage = "Internal error: should not reach here.";
		private const string CouldNotFindAddErrorMessage = "Could not find suitable Add method for the type {0}.";

		private static readonly Type[] ParameterTypes = new [] { typeof(ObjectReader), typeof(Int32) };
		private static readonly Type[] PrimitiveTypes = new [] {
			typeof(Guid), 
			typeof(Int64),
			typeof(UInt64),
			typeof(Int32),
			typeof(UInt32),
			typeof(Int16),
			typeof(UInt16),
			typeof(char),
			typeof(byte),
			typeof(bool),
			typeof(DateTime),
			typeof(TimeSpan),
			typeof(float),
			typeof(double)
		};
    }
}
