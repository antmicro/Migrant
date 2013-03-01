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
			//GenerateReadType();

			PushTypeOntoStack(typeToGenerate);
			generator.Emit(OpCodes.Ldc_I4_0);
		    GenerateReadObjectInner(typeToGenerate);

			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Ldc_I4_0);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0]));
			generator.Emit(OpCodes.Ret);

			/*
		    foreach (var field in fields)
		    {
				generator.Emit(OpCodes.Ldarg_0); // object reader
				generator.Emit(OpCodes.Ldfld, typeof(ObjectReader).GetField("reader", BindingFlags.NonPublic | BindingFlags.Instance));
		        var readMethod = typeof(PrimitiveReader).GetMethod(string.Concat("Read", field.FieldType.Name));
				if (readMethod == null)
				{
					throw new ArgumentException("Method not found");
				}
				
                generator.Emit(OpCodes.Call, readMethod);
				generator.Emit(OpCodes.Stfld, typeToGenerate.GetField(field.Name));
		    }
		
            generator.Emit(OpCodes.Ret);
            */
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

		private void GenerateReadObjectInner(Type formalType)
		{
			// method reads two arguments from stack
			// objId - identifier of object to create
			// objType - actual type of an object

		    var finish = generator.DefineLabel();
		    var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectTypeLocal = generator.DeclareLocal(typeof(Type));

			generator.Emit(OpCodes.Stloc, objectIdLocal);
			generator.Emit(OpCodes.Stloc, objectTypeLocal);

			generator.Emit(OpCodes.Ldloc, objectTypeLocal);
			generator.Emit(OpCodes.Ldloc, objectIdLocal);
			GenerateTouchObject(formalType);
			generator.Emit(OpCodes.Brtrue, finish); // if object has already been created skip initialization

			switch (ObjectReader.GetCreationWay(formalType))
		    {
                case ObjectReader.CreationWay.Null:
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
					GenerateReadNotPrecreated(formalType);
                    break;
                case ObjectReader.CreationWay.DefaultCtor:
                    //UpdateElements(actualType, objectId);
                    break;
                case ObjectReader.CreationWay.Uninitialized:
					generator.Emit(OpCodes.Ldloc, objectIdLocal);
                    GenerateUpdateFields(formalType);
                    break;
            }

			generator.MarkLabel(finish);
		}

		private void GenerateReadField(Type formalType)
		{
			// method returns read field value on stack

		    var finishLabel = generator.DefineLabel();
		    var updateMaximumReferenceIdLabel = generator.DefineLabel();
		    var alreadyDeserializedLabel = generator.DefineLabel();
			//var objectIdLocal = generator.DeclareLocal(typeof(Int32));

			//generator.Emit(OpCodes.Stloc, objectIdLocal);
		    
			if(!formalType.IsValueType)
            {
                var refId = generator.DeclareLocal(typeof(Int32));

                GenerateReadPrimitive(typeof(Int32)); // read object reference
				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Stloc, refId.LocalIndex);
				generator.Emit(OpCodes.Ldc_I4, AntMicro.Migrant.Consts.NullObjectId);
				generator.Emit(OpCodes.Beq, updateMaximumReferenceIdLabel);
				
				generator.Emit(OpCodes.Ldnull);
				generator.Emit(OpCodes.Br, finishLabel);

				generator.MarkLabel(updateMaximumReferenceIdLabel);

				generator.Emit(OpCodes.Ldloc, refId.LocalIndex);
				// GenerateUpdateMaximumReferenceId();

				// if(refId > nextObjectToRead) && !inlineRead.Contains(refId))
				generator.Emit(OpCodes.Ldloc, refId.LocalIndex);
				//generator.Emit(OpCodes.Ldloc, NEXT_OBJECT_TO_READ_LOACL_ID);
				generator.Emit(OpCodes.Ble, alreadyDeserializedLabel);

				//generator.Emit(OpCodes.Ldloc, INLINE_READ_LOCAL_ID);
				generator.Emit(OpCodes.Ldloc, refId.LocalIndex);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<HashSet<int>, bool>(x => x.Contains(0)));
				generator.Emit(OpCodes.Brtrue, alreadyDeserializedLabel);
				// ---

				//generator.Emit(OpCodes.Ldloc, INLINE_READ_LOCAL_ID);
				generator.Emit(OpCodes.Ldloc, refId.LocalIndex);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<HashSet<int>>(x => x.Add(0)));

				GenerateReadType();
				GenerateReadObjectInner(formalType, (short)refId.LocalIndex);

				generator.MarkLabel(alreadyDeserializedLabel);

				//generator.Emit(OpCodes.Ldloca, DESERIALIZED_OBJECTS_LOCAL_ID);
				generator.Emit(OpCodes.Ldloca, refId.LocalIndex);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0]));
		    }
			else
			{
				// value type
				GenerateReadPrimitive(formalType);
			}
			generator.MarkLabel(finishLabel);
		}

		private void GenerateUpdateFields(Type formalType)
		{
			// method reads one argument from stack
			// objId - identifier of an object to update

			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			generator.Emit(OpCodes.Stloc, objectIdLocal);

		    var fields = ObjectReader.GetFieldsToSerialize(formalType).ToList();
		    foreach (var field in fields)
		    {
				PushDeserializedObjectsCollectionOntoStack();
				generator.Emit(OpCodes.Ldloc, objectIdLocal);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<AutoResizingList<object>, object>(x => x[0]));
				GenerateReadField(field.FieldType);
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

		private void PushTypeOntoStack(Type type)
		{
            //var getTypeFromHandleMethod = typeof(Type).GetMethod("GetTypeFromHandle");
            generator.Emit(OpCodes.Ldtoken, type);
            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<RuntimeTypeHandle, Type>(o => Type.GetTypeFromHandle(o))); // loads value of <<typeToGenerate>> onto stack
        }

		private void GenerateTouchObject(Type formalType)
		{
			// method reads two values from stack:
			// objId - identifier of an object reference to touch
			// objType - actual type of an object to touch

			// method leaves int value onto stack:
			// 1 - the object has already been deserialized and shouldn't be recreated again
			// 0 - the object has been just created and should be properely initialized

		    var finish = generator.DefineLabel();
			var objNotYetDeserialized = generator.DefineLabel();
			var objectIdLocal = generator.DeclareLocal(typeof(Int32));
			var objectTypeLocal = generator.DeclareLocal(typeof(Type));

			generator.Emit(OpCodes.Stloc, objectIdLocal);
			generator.Emit(OpCodes.Stloc, objectTypeLocal);

			PushDeserializedObjectsCollectionOntoStack();
			generator.Emit(OpCodes.Call, Helpers.GetPropertyGetterInfo<AutoResizingList<object>, int>(x => x.Count)); // check current length of DeserializedObjectsCollection
			generator.Emit(OpCodes.Ldloc, objectIdLocal);
			generator.Emit(OpCodes.Ble, objNotYetDeserialized); // jump to object creation if objectId > DOC.Count
			generator.Emit(OpCodes.Ldc_I4_1); // push value <<1>> onto stack indicating that object has been deserialized earlier
			generator.Emit(OpCodes.Br, finish); // jump to the end

			generator.MarkLabel(objNotYetDeserialized);
			//generator.Emit(OpCodes.Ldloc, objectIdLocal);
			//GenerateUpdateMaximumReferenceId();

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

			generator.Emit(OpCodes.Ldc_I4_0); // push value <<0>> onto stack to indicate that object has just been created and needs initizlization
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
		    var readTypeMethod = typeof(ObjectReader).GetMethod("ReadType", BindingFlags.NonPublic | BindingFlags.Instance);

			generator.Emit(OpCodes.Ldarg_0); // object reader
			generator.Emit(OpCodes.Call, readTypeMethod);
			generator.Emit(OpCodes.Pop);
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
    }
}
