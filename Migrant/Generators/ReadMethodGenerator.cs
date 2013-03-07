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
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Text;
using AntMicro.Migrant;
using AntMicro.Migrant.Utilities;

namespace Migrant.Generators
{
    public class ReadMethodGenerator
    {
		public ReadMethodGenerator(Type typeToGenerate)
		{
		    dynamicMethod = new DynamicMethod("Read", typeToGenerate, ParameterTypes, true);
		    generator = dynamicMethod.GetILGenerator();

			GenerateDynamicCode(typeToGenerate);
			SaveToFile(typeToGenerate);
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
			GenerateInitializationCode();

		    GenerateReadObjectInner(typeToGenerate, false);

			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Ldc_I4_0);
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

		    if(formalType.IsValueType || formalType == typeof(string))
		    {
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit (OpCodes.Ldloc, objectIdLocal);
				GenerateReadPrimitive(formalType);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>>(x => x.SetItem(0, new object())));
			}
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
				generator.Emit(OpCodes.Ldc_I4_0);
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
                    throw new NotImplementedException();
                    break;
                case ObjectReader.CreationWay.Uninitialized:
					PushDeserializedObjectsCollectionOntoStack();
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0])); // pushes reference to an object to update (id is wrong due to structs)

                    GenerateUpdateFields(formalType);
                    break;
            }

			generator.MarkLabel(finish);
		}

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

		private bool GenerateReadField(FieldInfo finfo)
		{
			// method returns read field value on stack

			// NOTE: when read

			var formalType = finfo.FieldType;

		    var finishLabel = generator.DefineLabel();
		    var updateMaximumReferenceIdLabel = generator.DefineLabel();
			var else1 = generator.DefineLabel();
			var returnNullLabel = generator.DefineLabel();
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectActualTypeLocal = generator.DeclareLocal(typeof(Type));
			var metaResultLocal = generator.DeclareLocal(typeof(Int32));

			if (PrimitiveTypes.Contains(formalType))
			{
				// value type
				GenerateReadPrimitive(formalType);
				//generator.Emit(OpCodes.Br, finishLabel);
			}
			else if (formalType.IsEnum)
			{
				throw new NotImplementedException();
			}
			else if (formalType.IsValueType)
			{
				// here we have struct
				generator.Emit(OpCodes.Ldflda, finfo);
				GenerateUpdateStructFields(formalType);
				return false;
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

				if (formalType == typeof(string))
				{
					// special case
					GenerateReadPrimitive(typeof(string));
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

				generator.MarkLabel(finishLabel);
		    }

			return true;
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
				generator.Emit(OpCodes.Ldloc, objectLocal);
				if (GenerateReadField(field))
				{
					generator.Emit(OpCodes.Stfld, field);
				}
		    }
		}

		private void GenerateUpdateStructFields(Type formalType)
		{
			// method reads one argument from stack
			// obj - reference to an object to update
			
			var fields = ObjectReader.GetFieldsToSerialize(formalType).ToList();
			foreach (var field in fields)
			{
				generator.Emit(OpCodes.Dup);

				GenerateReadField(field);
				generator.Emit(OpCodes.Stfld, field);
			}
			generator.Emit(OpCodes.Pop);
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
				generator.Emit(OpCodes.Ldc_I4_0);
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
                new AssemblyName("dyn"), // call it whatever you want
                AssemblyBuilderAccess.Save);

            var dm = da.DefineDynamicModule("dyn_mod", "dyn.dll");
            var dt = dm.DefineType("dyn_type", TypeAttributes.Public);

            var method = dt.DefineMethod("Foo", MethodAttributes.Public | MethodAttributes.Static, ttg, ParameterTypes);

            generator = method.GetILGenerator();
			GenerateDynamicCode(ttg);

            dt.CreateType();
			
            da.Save("dyn.dll");
        }
		
		private ILGenerator generator;
		private DynamicMethod dynamicMethod;

        //private int REFERENCES_LOCAL_ID;
        //private int OBJECTS_CREATED_LOCAL_ID;
        //private int DESERIALIZED_OBJECTS_LOCAL_ID;
        //private int NEXT_OBJECT_TO_READ_LOACL_ID;
        //private int INLINE_READ_LOCAL_ID;

		private static readonly Type[] ParameterTypes = new [] { typeof(ObjectReader) };
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
