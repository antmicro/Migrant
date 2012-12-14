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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using AntMicro.Migrant.Hooks;
using ImpromptuInterface;
using AntMicro.Migrant.Generators;
using System.Reflection.Emit;
using System.Threading;

namespace AntMicro.Migrant
{
	/// <summary>
	/// Writes the object in a format that can be later read by <see cref="AntMicro.Migrant.ObjectReader"/>.
	/// </summary>
	public class ObjectWriter
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.ObjectWriter" /> class.
		/// </summary>
		/// <param name='stream'>
		/// Stream to which data will be written.
		/// </param>
		/// <param name='upfrontKnownTypes'>
		/// List of types that are considered to be known upfront, i.e. their type information is not written to the serialization stream.
		/// The <see cref="AntMicro.Migrant.ObjectReader" /> that will read such stream must receive consistent list.
		/// </param>
		/// <param name='preSerializationCallback'>
		/// Callback which is called once on every unique object before its serialization. Contains this object in its only parameter.
		/// </param>
		/// <param name='postSerializationCallback'>
		/// Callback which is called once on every unique object after its serialization. Contains this object in its only parameter.
		/// </param>
		/// <param name='writeMethodCache'>
		/// Cache in which generated write methods are stored and reused between instances of <see cref="AntMicro.Migrant.ObjectWriter" />.
		/// Can be null if one does not want to use the cache.
		/// </param>
		/// <param name='surrogatesForObjects'>
		/// Dictionary, containing callbacks that provide surrogate for given type. Callbacks have to be of type Func&lt;T, object&gt; where
		/// typeof(T) is given type.
		/// </param>			
		/// <param name='isGenerating'>
		/// True if write methods are to be generated, false if one wants to use reflection.
		/// </param>
		public ObjectWriter(Stream stream, IList<Type> upfrontKnownTypes, Action<object> preSerializationCallback = null, 
		                    Action<object> postSerializationCallback = null, IDictionary<Type, DynamicMethod> writeMethodCache = null,
		                    IDictionary<Type, Delegate> surrogatesForObjects = null, bool isGenerating = true)
		{
			if(surrogatesForObjects == null)
			{
				surrogatesForObjects = new Dictionary<Type, Delegate>();
			}
			transientTypeCache = new Dictionary<Type, bool>();
			writeMethods = new List<Action<PrimitiveWriter, object>>();
			postSerializationHooks = new List<Action>();
			this.writeMethodCache = writeMethodCache;
			this.isGenerating = isGenerating;
			this.surrogatesForObjects = surrogatesForObjects;
			typeIndices = new Dictionary<Type, int>();
			moduleIndices = new Dictionary<Module, int>();
			foreach(var type in upfrontKnownTypes)
			{
				AddMissingType(type);
			}
			this.stream = stream;
			this.preSerializationCallback = preSerializationCallback;
			this.postSerializationCallback = postSerializationCallback;
			PrepareForNextWrite();
		}

		/// <summary>
		/// Writes the given object along with the ones referenced by it.
		/// </summary>
		/// <param name='o'>
		/// The object to write.
		/// </param>
		public void WriteObject(object o)
		{
			objectsWritten = 0;
			identifier.GetId(o);
			while(identifier.Count > objectsWritten)
			{
				if(!inlineWritten.Contains(objectsWritten))
				{
					InvokeCallbacksAndWriteObject(identifier[objectsWritten]);
				}
				objectsWritten++;
			}
			foreach(var postHook in postSerializationHooks)
			{
				postHook();
			}
			PrepareForNextWrite();
		}

		internal void WriteObjectIdPossiblyInline(object o)
		{
			var refId = identifier.GetId(o);
			writer.Write(refId);
			if(ShouldBeInlined(refId))
			{
				inlineWritten.Add(refId);
				InvokeCallbacksAndWriteObject(o);
			}
		}

		internal Delegate[] GetDelegatesWithNonTransientTargets(MulticastDelegate mDelegate)
		{
			return mDelegate.GetInvocationList().Where(x => x.Target == null || !CheckTransient(x.Target)).ToArray();
		}

		internal bool CheckTransient(object o)
		{
			return CheckTransient(o.GetType());
		}

		internal bool CheckTransient(Type type)
		{
			bool result;
			if(transientTypeCache.TryGetValue(type, out result))
			{
				return result;
			}
			var isTransient = Helpers.CheckTransientNoCache(type);
			transientTypeCache.Add(type, isTransient);
			return isTransient;
		}

		internal static void CheckLegality(Type type)
		{
			if(type.IsPointer || type == typeof(IntPtr) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ThreadLocal<>)))
			{
				// TODO: message for the user should be more friendly if possible - it should contain path which lead to such field or type
				throw new InvalidOperationException("Pointer or ThreadLocal encountered during serialization.");
			}
		}

		internal int TouchAndWriteTypeId(Type type)
		{
			int typeId;
			if(typeIndices.ContainsKey(type))
			{
				typeId = typeIndices[type];
				writer.Write(typeId);
				return typeId;
			}
			AddMissingType(type);
			typeId = typeIndices[type];
			writer.Write(typeId);
			writer.Write(type.AssemblyQualifiedName);
			WriteModuleId(type.Module);
			return typeId;
		}

		internal void WriteModuleId(Module module)
		{
			int moduleId;
			if(moduleIndices.ContainsKey(module))
			{
				moduleId = moduleIndices[module];
				writer.Write(moduleId);
				return;
			}
			moduleId = nextModuleId++;
			writer.Write(moduleId);
			writer.Write(module.ModuleVersionId);
			moduleIndices.Add(module, moduleId);
		}

		internal static IEnumerable<FieldInfo> GetFieldsInSerializationOrder(Type type)
		{
			return type.GetAllFields().Where(Helpers.IsNotTransient).OrderBy(x => x.Name);
		}

		internal static bool HasSpecialWriteMethod(Type type)
		{
			return type == typeof(string) || typeof(ISpeciallySerializable).IsAssignableFrom(type) || Helpers.CheckTransientNoCache(type);
		}

		private void PrepareForNextWrite()
		{
			if(writer != null)
			{
				writer.Dispose();
			}
			identifier = new ObjectIdentifier();
			writer = new PrimitiveWriter(stream);
			inlineWritten = new HashSet<int>();
		}

		private void InvokeCallbacksAndWriteObject(object o)
		{
			if(preSerializationCallback != null)
			{
				preSerializationCallback(o);
			}
			WriteObjectInner(o);
			if(postSerializationCallback != null)
			{
				postSerializationCallback(o);
			}
		}

		private void WriteObjectInner(object o)
		{
			Helpers.SwapObjectWithSurrogate(ref o, surrogatesForObjects);
			var type = o.GetType();
			var typeId = TouchAndWriteTypeId(type);
			writeMethods[typeId](writer, o);
		}

		private void WriteObjectUsingReflection(PrimitiveWriter primitiveWriter, object o)
		{
			// the primitiveWriter parameter is not used here in fact, it is only to have
			// signature compatible with the generated method
			Helpers.InvokeAttribute(typeof(PreSerializationAttribute), o);
			var type = o.GetType();
			if(!WriteSpecialObject(o))
			{
				WriteObjectsFields(o, type);
			}
			Helpers.InvokeAttribute(typeof(PostSerializationAttribute), o);
			var postHook = Helpers.GetDelegateWithAttribute(typeof(LatePostSerializationAttribute), o);
			if(postHook != null)
			{
				postSerializationHooks.Add(postHook);
			}
		}

		private void WriteObjectsFields(object o, Type type)
		{
			// fields in the alphabetical order
			var fields = GetFieldsInSerializationOrder(type);
			foreach(var field in fields)
			{
				var fieldType = field.FieldType;
				var value = field.GetValue(o);
				WriteField(fieldType, value);
			}
		}

		private bool WriteSpecialObject(object o)
		{
			// if the object here is value type, it is in fact boxed
			// value type - the reference layout is fine, we should
			// write it using WriteField
			var type = o.GetType();
			if(type.IsValueType)
			{
				WriteField(type, o);
				return true;
			}
			var speciallySerializable = o as ISpeciallySerializable;
			if(speciallySerializable != null)
			{
				var startingPosition = writer.Position;
				speciallySerializable.Save(writer);
				writer.Write(writer.Position - startingPosition);
				return true;
			}
			if(type.IsArray)
			{
				var elementType = type.GetElementType();
				var array = o as Array;
				var rank = array.Rank;
				writer.Write(rank);
				WriteArray(elementType, array);
				return true;
			}
			var mDelegate = o as MulticastDelegate;
			if(mDelegate != null)
			{
				// if the target is trasient, we omit associated delegate entry
				var invocationList = GetDelegatesWithNonTransientTargets(mDelegate);
				writer.Write(invocationList.Length);
				foreach(var del in invocationList)
				{
					WriteField(typeof(object), del.Target);
					TouchAndWriteTypeId(del.Method.ReflectedType);
					writer.Write(del.Method.MetadataToken);
				}
				return true;
			}
			var str = o as string;
			if(str != null)
			{
				writer.Write(str);
				return true;
			}
			int count;
			// dictionary has precedence before collection
			Type formalKeyType;
			Type formalValueType;
			bool isGenericDictionary;
			if(Helpers.TryGetDictionaryCountAndElementTypes(o, out count, out formalKeyType, out formalValueType, out isGenericDictionary))
			{
				WriteDictionary(formalKeyType, formalValueType, count, o, isGenericDictionary);
				return true;
			}
			Type formalElementType;
			if(Helpers.TryGetCollectionCountAndElementType(o, out count, out formalElementType))
			{
				WriteEnumerable(formalElementType, count, (IEnumerable)o);
				return true;
			}
			return false;
		}

		private void WriteEnumerable(Type elementFormalType, int count, IEnumerable collection)
		{
			writer.Write(count);
			foreach(var element in collection)
			{
				WriteField(elementFormalType, element);
			}
		}

		private void WriteDictionary(Type formalKeyType, Type formalValueType, int count, object dictionary, bool isGenericDictionary)
		{
			writer.Write(count);
			if(isGenericDictionary)
			{
				var enumeratorMethod = typeof(IEnumerable<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(formalKeyType, formalValueType)).GetMethod("GetEnumerator");
				var enumerator = enumeratorMethod.Invoke(dictionary, null);
				var enumeratorType = enumeratorMethod.ReturnType;

				var moveNext = Helpers.GetMethodInfo<IEnumerator>(x => x.MoveNext());
				var currentField = enumeratorType.GetProperty("Current");
				var current = currentField.GetGetMethod();
				var currentType = current.ReturnType;
				var key = currentType.GetProperty("Key").GetGetMethod();
				var value = currentType.GetProperty("Value").GetGetMethod();
				while((bool)moveNext.Invoke(enumerator, null))
				{
					var currentValue = current.Invoke(enumerator, null);
					var keyValue = key.Invoke(currentValue, null);
					var valueValue = value.Invoke(currentValue, null);
					WriteField(formalKeyType, keyValue);
					WriteField(formalValueType, valueValue);
				}
			}
			else
			{
				var castDictionary = (IDictionary)dictionary;
				foreach(DictionaryEntry element in castDictionary)
				{
					WriteField(typeof(object), element.Key);
					WriteField(typeof(object), element.Value);
				}
			}
		}

		private void WriteArray(Type elementFormalType, Array array)
		{
			var rank = array.Rank;
			for(var i = 0; i < rank; i++)
			{
				writer.Write(array.GetLength(i));
			}
			var position = new int[rank];
			WriteArrayRowRecursive(array, 0, elementFormalType, position);
		}

		private void WriteArrayRowRecursive(Array array, int currentDimension, Type elementFormalType, int[] position)
		{
			var length = array.GetLength(currentDimension);
			for(var i = 0; i < length; i++)
			{
				if(currentDimension == array.Rank - 1)
				{
					// the final row
					WriteField(elementFormalType, array.GetValue(position));
				}
				else
				{
					WriteArrayRowRecursive(array, currentDimension + 1, elementFormalType, position);
				}
				position[currentDimension]++;
				for(var j = currentDimension + 1; j < array.Rank; j++)
				{
					position[j] = 0;
				}
			}
		}

		private void WriteField(Type formalType, object value)
		{
			var serializationType = Helpers.GetSerializationType(formalType);
			switch(serializationType)
			{
			case SerializationType.Transient:
				return;
			case SerializationType.Value:
				WriteValueType(formalType, value);
				return;
			case SerializationType.Reference:
				break;
			}
			// OK, so we should write a reference
			// a null reference maybe?
			if(value == null || CheckTransient(value))
			{
				writer.Write(Consts.NullObjectId);
				return;
			}
			var actualType = value.GetType();
			if(CheckTransient(actualType))
			{
				return;
			}
			CheckLegality(actualType);
			var refId = identifier.GetId(value);
			// if this is a future reference, just after the reference id,
			// we should write inline data
			writer.Write(refId);
			if(ShouldBeInlined(refId))
			{
				inlineWritten.Add(refId);
				InvokeCallbacksAndWriteObject(value);
			}
		}

		private bool ShouldBeInlined(int referenceId)
		{
			return referenceId > objectsWritten && !inlineWritten.Contains(referenceId);
		}

		private void WriteValueType(Type formalType, object value)
		{
			CheckLegality(formalType);
			// value type -> actual type is the formal type
			if(formalType.IsEnum)
			{
				writer.Write(Impromptu.InvokeConvert(value, Enum.GetUnderlyingType(formalType), true));
				return;
			}
			var nullableActualType = Nullable.GetUnderlyingType(formalType);
			if(nullableActualType != null)
			{
				if(value != null)
				{
					writer.Write(true);
					WriteValueType(nullableActualType, value);
				}
				else
				{
					writer.Write(false);
				}
				return;
			}
			if(formalType.IsPrimitive)
			{
				writer.Write((dynamic)value);
				return;
			}
			if(formalType == typeof(DateTime))
			{
				writer.Write((DateTime)value);
				return;
			}
			if(formalType == typeof(TimeSpan))
			{
				writer.Write((TimeSpan)value);
				return;
			}
			if(formalType == typeof(Guid))
			{
				writer.Write((Guid)value);
				return;
			}
			// so we guess it is struct
			WriteObjectsFields(value, formalType);
		}

		private void AddMissingType(Type type)
		{
			typeIndices.Add(type, nextTypeId++);
			Action<PrimitiveWriter, object> writeMethod = null; // for transient class the delegate will never be called
			if(!CheckTransient(type))
			{
				if(writeMethodCache != null && writeMethodCache.ContainsKey(type))
				{
					writeMethod = (Action<PrimitiveWriter, object>)writeMethodCache[type].CreateDelegate(typeof(Action<PrimitiveWriter, object>), this);
				}
				else
				{
					writeMethod = PrepareWriteMethod(type);
				}
			}
			writeMethods.Add(writeMethod);
		}

		private Action<PrimitiveWriter, object> PrepareWriteMethod(Type actualType)
		{
			var specialWrite = LinkSpecialWrite(actualType);
			if(specialWrite != null)
			{
				// linked methods are not added to writeMethodCache, there's no point
				return specialWrite;
			}

			if(!isGenerating)
			{
				return WriteObjectUsingReflection;
			}

			var method = new WriteMethodGenerator(actualType).Method;
			var result = (Action<PrimitiveWriter, object>)method.CreateDelegate(typeof(Action<PrimitiveWriter, object>), this);
			if(writeMethodCache != null)
			{
				writeMethodCache.Add(actualType, method);
			}
			return result;
		}

		private Action<PrimitiveWriter, object> LinkSpecialWrite(Type actualType)
		{
			if(actualType == typeof(string))
			{
				return (y, obj) => y.Write((string)obj);
			}
			if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
			{
				return (writer, obj) => {
					var startingPosition = writer.Position;
					((ISpeciallySerializable)obj).Save(writer);
					writer.Write(writer.Position - startingPosition);
				};
			}
			if(actualType == typeof(byte[]))
			{
				return (writer, objToWrite) => 
				{
					writer.Write(1); // rank of the array
					var array = (byte[])objToWrite;
					writer.Write(array.Length);
					writer.Write(array);
				};
			}
			return null;
		}

		private ObjectIdentifier identifier;
		private int objectsWritten;
		private int nextTypeId;
		private int nextModuleId;
		private PrimitiveWriter writer;
		private HashSet<int> inlineWritten;
		private readonly bool isGenerating;
		private readonly Stream stream;
		private readonly Action<object> preSerializationCallback;
		private readonly Action<object> postSerializationCallback;
		private readonly List<Action> postSerializationHooks;
		private readonly Dictionary<Type, int> typeIndices;
		private readonly Dictionary<Module, int> moduleIndices;
		private readonly Dictionary<Type, bool> transientTypeCache;
		private readonly IDictionary<Type, DynamicMethod> writeMethodCache;
		private readonly IDictionary<Type, Delegate> surrogatesForObjects;
		private readonly List<Action<PrimitiveWriter, object>> writeMethods;
	}
}

