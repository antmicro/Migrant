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
using System.Runtime.Serialization;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using AntMicro.Migrant.Hooks;
using AntMicro.Migrant.Utilities;
using ImpromptuInterface;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using Migrant.Generators;

namespace AntMicro.Migrant
{
	/// <summary>
	/// Reads the object previously written by <see cref="AntMicro.Migrant.ObjectWriter" />.
	/// </summary>
	public class ObjectReader
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.ObjectReader" /> class.
		/// </summary>
		/// <param name='stream'>
		/// Stream from which objects will be read.
		/// </param>
		/// <param name='upfrontKnownTypes'>
		/// List of types that are considered to be known upfront, i.e. their type information is not written to the serialization stream.
		/// It has to be consistent with <see cref="AntMicro.Migrant.ObjectWriter" /> that wrote the stream.
		/// </param>
		/// <param name='ignoreModuleIdInequality'>
		/// If set to true, deserialization continues even if the module id used for deserialization differs from the one used for serialization.
		/// If set to false, exception is thrown instead.
		/// </param>
		/// <param name='objectsForSurrogates'>
		/// Dictionary, containing callbacks that provide objects for given type of surrogate. Callbacks have to be of type Func&lt;T, object&gt; where
		/// typeof(T) is type of surrogate.
		/// </param>
		/// <param name='postDeserializationCallback'>
		/// Callback which will be called after deserialization of every unique object. Deserialized
		/// object is given in the callback's only parameter.
		/// </param>
		/// <param name='readMethods'>
		/// Cache in which generated read methods are stored and reused between instances of <see cref="AntMicro.Migrant.ObjectReader" />.
		/// Can be null if one does not want to use the cache.
		/// </param>
		/// <param name='isGenerating'>
		/// True if read methods are to be generated, false if one wants to use reflection.
		/// </param>
		public ObjectReader(Stream stream, IList<Type> upfrontKnownTypes, bool ignoreModuleIdInequality, IDictionary<Type, Delegate> objectsForSurrogates = null,
		                    Action<object> postDeserializationCallback = null, IDictionary<Type, DynamicMethod> readMethods = null, bool isGenerating = false)
		{
			if(objectsForSurrogates == null)
			{
				objectsForSurrogates = new Dictionary<Type, Delegate>();
			}
			this.objectsForSurrogates = objectsForSurrogates;
			this.ignoreModuleIdInequality = ignoreModuleIdInequality;
			this.readMethodsCache = readMethods ?? new Dictionary<Type, DynamicMethod>();
			this.useGeneratedDeserialization = isGenerating;
			typeList = new List<Type>();
			postDeserializationHooks = new List<Action>();
			agreedModuleIds = new HashSet<int>();
			for(var i = 0; i < upfrontKnownTypes.Count; i++)
			{
				typeList.Add(upfrontKnownTypes[i]);
				EnsureReadMethod(upfrontKnownTypes[i]);
			}
			this.stream = stream;
			this.postDeserializationCallback = postDeserializationCallback;
			PrepareForTheRead();
		}

		/// <summary>
		/// Reads the object with the expected formal type <typeparam name='T'/>.
		/// </summary>
		/// <returns>
		/// The object, previously written by the <see cref="AntMicro.Migrant.ObjectWriter" />.
		/// </returns>
		/// <typeparam name='T'>
		/// The expected formal type of object, that is the type of the reference returned
		/// by the method after serialization. The previously serialized object must be
		/// convertible to this type.
		/// </typeparam>
		/// <remarks>
		/// Note that this method will read the object from the stream along with other objects
		/// referenced by it.
		/// </remarks>
		public T ReadObject<T>()
		{
			var type = ReadType();
			if(useGeneratedDeserialization)
			{
				ReadObjectInnerGenerated(type, 0);
			}
			else
			{
				ReadObjectInner(type, 0);
			}

			var obj = deserializedObjects[0];
			if(!(obj is T))
			{
				throw new InvalidDataException(
					string.Format("Type {0} requested to deserialize, however type {1} encountered in the stream.",
				              typeof(T), obj.GetType()));
			}
			
			PrepareForTheRead();
			foreach(var hook in postDeserializationHooks)
			{
				hook();
			}
			return (T)obj;
		}

		private void EnsureReadMethod(Type type)
		{
			if(!readMethodsCache.ContainsKey(type))
			{
				var rmg = new ReadMethodGenerator(type);
				readMethodsCache.Add(type, rmg.Method);
			}
		}

		private void PrepareForTheRead()
		{
			if(reader != null)
			{
				reader.Dispose();
			}

			delegatesCache = new Dictionary<Type, Func<int, object>>();
			deserializedObjects = new AutoResizingList<object>(InitialCapacity);
			reader = new PrimitiveReader(stream);
		}

		internal static bool HasSpecialReadMethod(Type type)
		{
			return type == typeof(string) || typeof(ISpeciallySerializable).IsAssignableFrom(type) || Helpers.CheckTransientNoCache(type);
		}

		internal void ReadObjectInnerGenerated(Type actualType, int objectId)
		{
			EnsureReadMethod(actualType);
			if(!delegatesCache.ContainsKey(actualType))
			{
				var func = (Func<Int32, object>)readMethodsCache[actualType].CreateDelegate(typeof(Func<Int32, object>), this);
				delegatesCache.Add(actualType, func);
			}

			// execution of read method of given type
			deserializedObjects[objectId] = delegatesCache[actualType](objectId);
		}

		private void ReadObjectInner(Type actualType, int objectId)
		{
			TouchObject(actualType, objectId);
			switch(GetCreationWay(actualType))
			{
			case CreationWay.Null:
				ReadNotPrecreated(actualType, objectId);
				break;
			case CreationWay.DefaultCtor:
				UpdateElements(actualType, objectId);
				break;
			case CreationWay.Uninitialized:
				UpdateFields(actualType, deserializedObjects[objectId]);
				break;
			}
			var obj = deserializedObjects[objectId];
			if(obj == null)
			{
				// it can happen if we deserialize delegate with empty invocation list
				return;
			}
			Helpers.SwapObjectWithSurrogate(ref obj, objectsForSurrogates);
			deserializedObjects[objectId] = obj; // could be swapped
			Helpers.InvokeAttribute(typeof(PostDeserializationAttribute), obj);
			var postHook = Helpers.GetDelegateWithAttribute(typeof(LatePostDeserializationAttribute), obj);
			if(postHook != null)
			{
				postDeserializationHooks.Add(postHook);
			}
			if(postDeserializationCallback != null)
			{
				postDeserializationCallback(obj);
			}
		}

		private void UpdateFields(Type actualType, object target)
		{
			var fields = GetFieldsToSerialize(actualType);
			foreach(var field in fields)
			{
				if(field.IsDefined(typeof(TransientAttribute), false))
				{
					if(field.IsDefined(typeof(ConstructorAttribute), false))
					{
						var ctorAttribute = (ConstructorAttribute)field.GetCustomAttributes(false).First(x => x is ConstructorAttribute);
						field.SetValue(target, Impromptu.InvokeConstructor(field.FieldType, ctorAttribute.Parameters));
					}
					continue;
				}
				field.SetValue(target, ReadField(field.FieldType));
			}
		}

		private void ReadNotPrecreated(Type type, int objectId)
		{
			if(type.IsValueType)
			{
				// a boxed value type
				deserializedObjects[objectId] = ReadField(type);
			}
			else
			if(type == typeof(string))
			{
				deserializedObjects[objectId] = reader.ReadString();
			}
			else
			if(type.IsArray)
			{
				ReadArray(type.GetElementType(), objectId);
			}
			else
			if(typeof(MulticastDelegate).IsAssignableFrom(type))
			{
				ReadDelegate(type, objectId);
			}
			else
			if(type.IsGenericType && typeof(ReadOnlyCollection<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
			{
				ReadReadOnlyCollection(type, objectId);
			}
			else
			{
				throw new InvalidOperationException(InternalErrorMessage);
			}
		}

		private void UpdateElements(Type type, int objectId)
		{
			var obj = deserializedObjects[objectId];
			var speciallyDeserializable = obj as ISpeciallySerializable;
			if(speciallyDeserializable != null)
			{
				LoadAndVerifySpeciallySerializableAndVerify(speciallyDeserializable, reader);
				return;
			}
			Type formalKeyType, formalValueType;
			bool isGenericDictionary;
			if(Helpers.IsDictionary(type, out formalKeyType, out formalValueType, out isGenericDictionary))
			{
				FillDictionary(formalKeyType, formalValueType, obj);
				return;
			}
			Type elementFormalType;
			bool fake, fake2, fake3;
			if(!Helpers.IsCollection(type, out elementFormalType, out fake, out fake2, out fake3))
			{
				throw new InvalidOperationException(InternalErrorMessage);
			}

			// so we can assume it is ICollection<T> or ICollection
			FillCollection(elementFormalType, obj);
		}

		private object ReadField(Type formalType)
		{
			if(Helpers.CheckTransientNoCache(formalType))
			{
				return Helpers.GetDefaultValue(formalType);
			}

			if(!formalType.IsValueType)
			{
				var refId = reader.ReadInt32();
				if(refId == Consts.NullObjectId)
				{
					return null;
				}
				if(refId >= deserializedObjects.Count)
				{
					ReadObjectInner(ReadType(), refId);
				}
				return deserializedObjects[refId];
			} 
			if(formalType.IsEnum)
			{
				var value = ReadField(Enum.GetUnderlyingType(formalType));
				return Enum.ToObject(formalType, Impromptu.InvokeConvert(value, typeof(long), true));
			}
			var nullableActualType = Nullable.GetUnderlyingType(formalType);
			if(nullableActualType != null)
			{
				var isNotNull = reader.ReadBoolean();
				return isNotNull ? ReadField(nullableActualType) : null;
			}
			if(Helpers.IsWriteableByPrimitiveWriter(formalType))
			{
				var methodName = string.Concat("Read", formalType.Name);
				var readMethod = typeof(PrimitiveReader).GetMethod(methodName);
				return readMethod.Invoke(reader, Type.EmptyTypes);
			}
			var returnedObject = Activator.CreateInstance(formalType);
			// here we have a boxed struct which we put to struct reference list
			UpdateFields(formalType, returnedObject);
			// if this is subtype
			return returnedObject;
		}

		private void FillCollection(Type elementFormalType, object obj)
		{
			var collectionType = obj.GetType();
			var count = reader.ReadInt32();
			var addMethod = collectionType.GetMethod("Add", new [] { elementFormalType }) ??
				collectionType.GetMethod("Enqueue", new [] { elementFormalType }) ??
				collectionType.GetMethod("Push", new [] { elementFormalType });
			if(addMethod == null)
			{
				throw new InvalidOperationException(string.Format(CouldNotFindAddErrorMessage,
                                                                  collectionType));
			}
			Type delegateType;
			if(addMethod.ReturnType == typeof(void))
			{
				delegateType = typeof(Action<>).MakeGenericType(new [] { elementFormalType });
			}
			else
			{
				delegateType = typeof(Func<,>).MakeGenericType(new [] {
					elementFormalType,
					addMethod.ReturnType
				});
			}
			var addDelegate = Delegate.CreateDelegate(delegateType, obj, addMethod);
			if(collectionType == typeof(Stack) || 
				(collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(Stack<>)))
			{
				var stack = (dynamic)obj;
				var temp = new dynamic[count];
				for(var i = 0; i < count; i++)
				{
					temp[i] = ReadField(elementFormalType);
				}
				for(var i = count - 1; i >= 0; --i)
				{
					stack.Push(temp[i]);
				}
			}
			else
			{
				for(var i = 0; i < count; i++)
				{
					var fieldValue = ReadField(elementFormalType);
					addDelegate.FastDynamicInvoke(fieldValue);
				}
			}
		}

		private void FillDictionary(Type formalKeyType, Type formalValueType, object obj)
		{
			var dictionaryType = obj.GetType();
			var count = reader.ReadInt32();
			var addMethodArgumentTypes = new [] {
				formalKeyType,
				formalValueType
			};
			var addMethod = dictionaryType.GetMethod("Add", addMethodArgumentTypes) ??
				dictionaryType.GetMethod("TryAdd", addMethodArgumentTypes);
			if(addMethod == null)
			{
				throw new InvalidOperationException(string.Format(CouldNotFindAddErrorMessage,
                                                                  dictionaryType));
			}
			Type delegateType;
			if(addMethod.ReturnType == typeof(void))
			{
				delegateType = typeof(Action<,>).MakeGenericType(addMethodArgumentTypes);
			}
			else
			{
				delegateType = typeof(Func<,,>).MakeGenericType(new [] {
					formalKeyType,
					formalValueType,
					addMethod.ReturnType
				});
			}
			var addDelegate = Delegate.CreateDelegate(delegateType, obj, addMethod);

			for(var i = 0; i < count; i++)
			{
				var key = ReadField(formalKeyType);
				var value = ReadField(formalValueType);
				addDelegate.FastDynamicInvoke(key, value);
			}
		}

		private void ReadArray(Type elementFormalType, int objectId)
		{
			var rank = reader.ReadInt32();
			var lengths = new int[rank];
			for(var i = 0; i < rank; i++)
			{
				lengths[i] = reader.ReadInt32();
			}
			var array = Array.CreateInstance(elementFormalType, lengths);
			// we should update the array object as soon as we can
			// why? because it can have the reference to itself (what a corner case!)
			deserializedObjects[objectId] = array;
			var position = new int[rank];
			FillArrayRowRecursive(array, 0, position, elementFormalType);
		}

		private void ReadDelegate(Type type, int objectId)
		{
			var invocationListLength = reader.ReadInt32();
			for(var i = 0; i < invocationListLength; i++)
			{
				var target = ReadField(typeof(object));
				var containingType = ReadType();
				// constructor cannot be bound to delegate, so we can just cast to methodInfo
				var method = (MethodInfo)containingType.Module.ResolveMethod(reader.ReadInt32());
				var del = Delegate.CreateDelegate(type, target, method);
				deserializedObjects[objectId] = Delegate.Combine((Delegate)deserializedObjects[objectId], del);
			}
		}

		private void ReadReadOnlyCollection(Type type, int objectId)
		{
			var elementFormalType = type.GetGenericArguments()[0];
			var length = reader.ReadInt32();
			var array = Array.CreateInstance(elementFormalType, length);
			for(var i = 0; i < length; i++)
			{
				array.SetValue(ReadField(elementFormalType), i);
			}
			deserializedObjects[objectId] = Activator.CreateInstance(type, array);
		}

		private void FillArrayRowRecursive(Array array, int currentDimension, int[] position, Type elementFormalType)
		{
			var length = array.GetLength(currentDimension);
			for(var i = 0; i < length; i++)
			{
				if(currentDimension == array.Rank - 1)
				{
					var value = ReadField(elementFormalType);
					array.SetValue(value, position);
				}
				else
				{
					FillArrayRowRecursive(array, currentDimension + 1, position, elementFormalType);
				}
				position[currentDimension]++;
				for(var j = currentDimension + 1; j < array.Rank; j++)
				{
					position[j] = 0;
				}
			}
		}

		internal static void LoadAndVerifySpeciallySerializableAndVerify(ISpeciallySerializable obj, PrimitiveReader reader)
		{
			var beforePosition = reader.Position;
			obj.Load(reader);
			var afterPosition = reader.Position;
			var serializedLength = reader.ReadInt64();
			if(serializedLength + beforePosition != afterPosition)
			{
				throw new InvalidOperationException(string.Format(
					"Stream corruption by '{0}', incorrent magic {1} when {2} expected.", obj.GetType(), serializedLength, afterPosition - beforePosition));
			}
		}

		internal Type ReadType()
		{
			var typeId = reader.ReadInt32();
			if(typeId == Consts.NullObjectId)
			{
				return null;
			}
			if(typeList.Count <= typeId)
			{
				var typeName = reader.ReadString();
				typeList.Add(Type.GetType(typeName));
			}
			else
			{
				return typeList[typeId];
			}
			var moduleId = reader.ReadInt32();
			if(!agreedModuleIds.Contains(moduleId))
			{
				var type = typeList[typeId];
				var oldMvid = reader.ReadGuid();
				var module = type.Module;
				var newMvid = module.ModuleVersionId;
				if(oldMvid != newMvid && !ignoreModuleIdInequality)
				{
					throw new InvalidOperationException(
						string.Format("Cannot deserialize data. The type '{0}' in module {1} was serialized with mvid {2}, but you are trying to deserialize it using mvid {3}.",
					              type, module, oldMvid, newMvid));
				}
				agreedModuleIds.Add(moduleId);
			}
			return typeList[typeId];
		}

		private object TouchObject(Type actualType, int refId)
		{
			if(deserializedObjects[refId] != null)
			{
				return deserializedObjects[refId];
			}

			object created = null;
			switch(GetCreationWay(actualType))
			{
			case CreationWay.Null:
				break;
			case CreationWay.DefaultCtor:
				created = Activator.CreateInstance(actualType);
				break;
			case CreationWay.Uninitialized:
				created = FormatterServices.GetUninitializedObject(actualType);
				break;
			}
			deserializedObjects[refId] = created;
			return created;
		}

		internal static CreationWay GetCreationWay(Type actualType)
		{
			if(Helpers.CanBeCreatedWithDataOnly(actualType))
			{
				return CreationWay.Null;
			}
			Type fake;
			bool fake2, fake3, fake4;
			if(Helpers.IsCollection(actualType, out fake, out fake2, out fake3, out fake4))
			{
				return CreationWay.DefaultCtor;
			}
			if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
			{
				return CreationWay.DefaultCtor;
			}
			return CreationWay.Uninitialized;
		}

		internal static IEnumerable<FieldInfo> GetFieldsToSerialize(Type actualType)
		{
			var fields = actualType.GetAllFields().Where(x => !x.Attributes.HasFlag(FieldAttributes.Literal))
                .OrderBy(x => x.Name);
			return fields;
		}

		private bool useGeneratedDeserialization;
		internal AutoResizingList<object> deserializedObjects;
		private IDictionary<Type, DynamicMethod> readMethodsCache;
		private Dictionary<Type, Func<Int32, object>> delegatesCache;
		internal PrimitiveReader reader;
		private readonly List<Type> typeList;
		private readonly HashSet<int> agreedModuleIds;
		private readonly Stream stream;
		private readonly bool ignoreModuleIdInequality;
		internal readonly Action<object> postDeserializationCallback;
		internal readonly List<Action> postDeserializationHooks;
		internal readonly IDictionary<Type, Delegate> objectsForSurrogates;
		private const int InitialCapacity = 128;
		private const string InternalErrorMessage = "Internal error: should not reach here.";
		private const string CouldNotFindAddErrorMessage = "Could not find suitable Add method for the type {0}.";

		internal enum CreationWay
		{
			Uninitialized,
			DefaultCtor,
			Null
		}
	}
}

