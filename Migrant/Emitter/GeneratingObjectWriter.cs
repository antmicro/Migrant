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
using AntMicro.Migrant;
using System.IO;
using System.Collections.Generic;
using System;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.Emitter
{
	public class GeneratingObjectWriter : ObjectWriter
	{
		public GeneratingObjectWriter(Stream stream, IDictionary<Type, int> typeIndices, bool strictTypes, Action<Type> missingTypeCallback = null, 
		                              Action<object> preSerializationCallback = null, Action<object> postSerializationCallback = null)
			: base(stream, typeIndices, strictTypes, missingTypeCallback, preSerializationCallback, postSerializationCallback)
		{
			transientTypes = new Dictionary<Type, bool>();
			writeMethods = new Action<object, PrimitiveWriter, GeneratingObjectWriter>[0];
			RegenerateWriteMethods();
		}

		internal void WriteObjectId(object o)
		{
			// this function is called when object to serialize cannot be data-inlined object such as string
			Writer.Write(Identifier.GetId(o));
		}

		internal void WriteObjectIdPossiblyInline(object o)
		{
			var refId = Identifier.GetId(o);
			var type = o.GetType();
			Writer.Write(refId);
			if(ShouldBeInlined(type, refId))
			{
				InlineWritten.Add(refId);
                InvokeCallbacksAndWriteObject(o);
			}
		}

		// TODO: inline?
		internal bool CheckTransient(object o)
		{
			return CheckTransient(o.GetType());
		}

		internal bool CheckTransient(Type type)
		{
			bool result;
			if(transientTypes.TryGetValue(type, out result))
			{
				return result;
			}
			var isTransient = type.IsDefined(typeof(TransientAttribute), false);
			transientTypes.Add(type, isTransient);
			return isTransient;
		}

		protected internal override void WriteObjectInner(object o)
		{
			var type = o.GetType();
            // TODO: touch should refresh array
            TouchType(type);
			var typeId = TypeIndices[type];
			Writer.Write(typeId);
			writeMethods[typeId](o, Writer, this);
		}

		protected override void AddMissingType(Type type)
		{
			base.AddMissingType(type);
			RegenerateWriteMethods();
		}

		private void RegenerateWriteMethods()
		{
			var newWriteMethods = new Action<object, PrimitiveWriter, GeneratingObjectWriter>[TypeIndices.Count];
			foreach(var entry in TypeIndices)
			{
				if(writeMethods.Length > entry.Value)
				{
					newWriteMethods[entry.Value] = writeMethods[entry.Value];
				}
				else
				{
					if(!CheckTransient(entry.Key))
					{
						newWriteMethods[entry.Value] = GenerateWriteMethod(entry.Key);
					}
					// for transient class the delegate will never be called
				}
			}
			writeMethods = newWriteMethods;
		}

		private Action<object, PrimitiveWriter, GeneratingObjectWriter> GenerateWriteMethod(Type actualType)
		{
			var specialWrite = LinkSpecialWrite(actualType);
			if(specialWrite != null)
			{
				return specialWrite;
			}

			// TODO: callbacks!!
			// TODO: parameter types: move and unify
			var method = new DynamicMethod("Write", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard,
			                               typeof(void), new [] { typeof(object), typeof(PrimitiveWriter), typeof(GeneratingObjectWriter) }, actualType, true);
			var generator = method.GetILGenerator();
			if(!GenerateSpecialWrite(generator, actualType))
			{
				var fields = actualType.GetAllFields().Where(Helpers.IsNotTransient).OrderBy(x => x.Name); // TODO: unify
				foreach(var field in fields)
				{
					GenerateWriteType(generator, gen => 
					                  {
						gen.Emit(OpCodes.Ldarg_0); // object to serialize
						gen.Emit(OpCodes.Ldfld, field); // TODO: consider making local variable
					}, field.FieldType);
				}
			}
			generator.Emit(OpCodes.Ret);
			var result = (Action<object, PrimitiveWriter, GeneratingObjectWriter>)method.CreateDelegate(typeof(Action<object, PrimitiveWriter, GeneratingObjectWriter>));
			return result;
		}

		private Action<object, PrimitiveWriter, GeneratingObjectWriter> LinkSpecialWrite(Type actualType)
		{
			if(actualType == typeof(string))
			{
				return (x, y, @this) => y.Write((string)x);
			}
			if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
			{
				return (x, writer, @this) => {
					var startingPosition = writer.Position;
	                ((ISpeciallySerializable)x).Save(writer);
	                writer.Write(writer.Position - startingPosition);
				};
			}
			return null;
		}

		private bool GenerateSpecialWrite(ILGenerator generator, Type actualType)
		{
			// TODO: value type
			if(actualType.IsArray)
			{
				GenerateWriteArray(generator, actualType);
				return true;
			}
			return false;
		}

		private void GenerateWriteArray(ILGenerator generator, Type actualType)
		{
			PrimitiveWriter primitiveWriter = null; // TODO

			var rank = actualType.GetArrayRank();
			if(rank != 1)
			{
				throw new NotImplementedException();
			}

			var elementType = actualType.GetElementType();

			generator.DeclareLocal(typeof(int)); // this is for counter
			generator.DeclareLocal(elementType); // this is for the current element
			generator.DeclareLocal(typeof(int)); // length of the array

			// writing rank
			generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
			generator.Emit(OpCodes.Ldc_I4, rank);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => primitiveWriter.Write(0)));

			// writing length
			generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
			generator.Emit(OpCodes.Ldarg_0); // array to serialize
			generator.Emit(OpCodes.Ldlen);
			generator.Emit(OpCodes.Dup);
			generator.Emit(OpCodes.Stloc_2);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => primitiveWriter.Write(0)));

			// writing elements
			generator.Emit(OpCodes.Ldc_I4_0);
			generator.Emit(OpCodes.Stloc_0);
			var loopBegin = generator.DefineLabel();
			generator.MarkLabel(loopBegin);
			generator.Emit(OpCodes.Ldarg_0); // array to serialize
			generator.Emit(OpCodes.Ldloc_0); // index
			generator.Emit(OpCodes.Ldelem, elementType);
			generator.Emit(OpCodes.Stloc_1); // we put current element to local variable

			GenerateWriteType(generator, gen =>
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

		private void GenerateWriteType(ILGenerator generator, Action<ILGenerator> putValueToWriteOnTop, Type formalType)
		{
			switch(Helpers.GetSerializationType(formalType))
			{
			case SerializationType.Transient:
				// just omit it
				return;
			case SerializationType.Value:
				GenerateWriteValue(generator, putValueToWriteOnTop, formalType);
				break;
			case SerializationType.Reference:
				GenerateWriteReference(generator, putValueToWriteOnTop, formalType);
				break;
			}
		}

		private void GenerateWriteValue(ILGenerator generator, Action<ILGenerator> putValueToWriteOnTop, Type formalType)
		{
			// TODO: structs
			generator.Emit(OpCodes.Ldarg_1); // primitive writer waits there
			putValueToWriteOnTop(generator);
			generator.Emit(OpCodes.Call, typeof(PrimitiveWriter).GetMethod("Write", new [] { formalType }));
		}

		private void GenerateWriteReference(ILGenerator generator, Action<ILGenerator> putValueToWriteOnTop, Type formalType)
		{
			ObjectWriter baseWriter = null; // TODO: fake, maybe promote to field
			PrimitiveWriter primitiveWriter = null; // TODO: as above
			object nullObject = null; // TODO: as above

			var finish = generator.DefineLabel();

			putValueToWriteOnTop(generator);
			var isNotNull = generator.DefineLabel();
			generator.Emit(OpCodes.Brtrue_S, isNotNull);
			generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
			generator.Emit(OpCodes.Ldc_I4_M1); // TODO: Consts value
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => primitiveWriter.Write(0)));
			generator.Emit(OpCodes.Br, finish);
			generator.MarkLabel(isNotNull);

			var formalTypeIsActualType = formalType.Attributes.HasFlag(TypeAttributes.Sealed); // TODO: more optimizations?
			if(formalTypeIsActualType)
			{
				var typeId = TypeIndices[formalType];
				generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
				generator.Emit(OpCodes.Ldc_I4, typeId);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => primitiveWriter.Write(0))); // TODO: get it once
			}
			else
			{
				// we have to get the actual type at runtime
				generator.Emit(OpCodes.Ldarg_1); // primitiveWriter
				generator.Emit(OpCodes.Ldarg_2); // objectWriter
				putValueToWriteOnTop(generator);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => baseWriter.ObjectToTypeId(null))); // TODO: better do type to type id
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => primitiveWriter.Write(0))); // TODO: get it once
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
				generator.Emit(OpCodes.Ldarg_2); // objectWriter
				putValueToWriteOnTop(generator);
				generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => CheckTransient(nullObject)));
				generator.Emit(OpCodes.Brtrue_S, finish);
			}

			if(!skipGetId)
			{
				// if the formal type is NOT object, then string or array will not be the content of the field
				var mayBeInlined = formalType == typeof(object) || Helpers.CanBeCreatedWithDataOnly(formalType);
				generator.Emit(OpCodes.Ldarg_2); // objectWriter
				putValueToWriteOnTop(generator);
				if(mayBeInlined)
				{
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => WriteObjectIdPossiblyInline(null)));
				}
				else
				{
					generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => WriteObjectId(null)));
				}
			}
			generator.MarkLabel(finish);
		}

		private readonly Dictionary<Type, bool> transientTypes;
		private Action<object, PrimitiveWriter, GeneratingObjectWriter>[] writeMethods;
	}
}

