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
		/// <param name='typeArray'>
		/// Array which is used to resolve type using type ID. Must be consisent with the type ID
		/// dictionary used by the <see cref="AntMicro.Migrant.ObjectWriter" />  which wrote the data.
		/// </param>
		/// <param name='postDeserializationCallback'>
		/// Callback which will be called after deserialization of every unique object. Deserialized
		/// object is given in the callback's only parameter.
		/// </param>
        public ObjectReader(Stream stream, Type[] typeArray, Action<object> postDeserializationCallback = null)
        {
            reader = new PrimitiveReader(stream);
            this.typeArray = typeArray;
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
            var typeIndex = reader.ReadInt32();
            ReadObjectInner(typeIndex, 0);
            nextObjectToRead++;
            var obj = deserializedObjects[0];
            if(!(obj is T))
            {
                throw new InvalidDataException();
            }
            while(objectsCreated >= nextObjectToRead)
            {
                if(!inlineRead.Contains(nextObjectToRead))
                {
                    typeIndex = reader.ReadInt32();
                    ReadObjectInner(typeIndex, nextObjectToRead);
                }
                nextObjectToRead++;
            }
            PrepareForTheRead();
            return (T)obj;
        }

        private void PrepareForTheRead()
        {
            if(reader != null)
            {
                reader.Dispose();
            }
            nextObjectToRead = 0;
            objectsCreated = 0;
            deserializedObjects = new AutoResizingList<object>(InitialCapacity);
            inlineRead = new HashSet<int>();
            reader = new PrimitiveReader(stream);
        }

        private void ReadObjectInner(int typeIndex, int objectId)
        {
            TouchObject(typeIndex, objectId);
            var actualType = typeArray[typeIndex];
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
            Helpers.InvokeAttribute(typeof(PostDeserializationAttribute), obj);
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
            else if(type == typeof(string))
            {
                deserializedObjects[objectId] = reader.ReadString();
            }
            else if(type.IsArray)
            {
                ReadArray(type.GetElementType(), objectId);
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
                var beforePosition = reader.Position;
                speciallyDeserializable.Load(reader);
                var afterPosition = reader.Position;
                var serializedLength = reader.ReadInt64();
                if(serializedLength + beforePosition != afterPosition)
                {
                    throw new InvalidOperationException(string.Format(
                        "Stream corruption by '{0}', {1} bytes was read.", obj, serializedLength));
                }
                return;
            }
            Type formalKeyType, formalValueType;
            if(Helpers.IsDictionary(type, out formalKeyType, out formalValueType))
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
            if(typeof(IList).IsAssignableFrom(type))
            {
                FillList(elementFormalType, (IList)obj);
                return;
            }
            // so we can assume it is ICollection<T> or ICollection
            FillCollection(elementFormalType, obj);
        }

        private object ReadField(Type formalType)
        {
            if(formalType.IsDefined(typeof(TransientAttribute), false))
            {
                return Helpers.GetDefaultValue(formalType);
            }
            if(!formalType.IsValueType)
            {
                var typeId = reader.ReadInt32();
                if(typeId == Consts.NullObjectId)
                {
                    return null;
                }
                var actualType = typeArray[typeId];
                if(actualType.IsDefined(typeof(TransientAttribute), false))
                {
                    return Helpers.GetDefaultValue(formalType);
                }
                var refId = reader.ReadInt32();
                UpdateMaximumReferenceId(refId);
                if(Helpers.CanBeCreatedWithDataOnly(actualType) && refId > nextObjectToRead && !inlineRead.Contains(refId))
                {
                    // future reference, data inlined
                    inlineRead.Add(refId);
                    var typeIndex = reader.ReadInt32();
                    ReadObjectInner(typeIndex, refId);
                    return deserializedObjects[refId];
                }
                return TouchObject(typeId, refId);
            } 
            if(formalType.IsEnum)
            {
                return Enum.ToObject(formalType, reader.ReadInt64());
            }
            var nullableActualType = Nullable.GetUnderlyingType(formalType);
            if(nullableActualType != null)
            {
                var isNotNull = reader.ReadBool();
                return isNotNull ? ReadField(nullableActualType) : null;
            }
            if(formalType == typeof(Int64))
            {
               return reader.ReadInt64();
            } 
            if(formalType == typeof(UInt64))
            {
                return reader.ReadUInt64();
            }
            if(formalType == typeof(Int32))
            {
               return reader.ReadInt32();
            } 
            if(formalType == typeof(UInt32))
            {
                return reader.ReadUInt32();
            } 
            if(formalType == typeof(Int16))
            {
                return reader.ReadInt16();
            } 
            if(formalType == typeof(UInt16))
            {
                return reader.ReadUInt16();
            } 
            if(formalType == typeof(char))
            {
                return reader.ReadChar();
            }
            if(formalType == typeof(byte))
            {
                return reader.ReadByte();
            }
            if(formalType == typeof(bool))
            {
                return reader.ReadBool();
            }
            if(formalType==typeof(DateTime))
            {
                return reader.ReadDateTime();
            }
            if(formalType==typeof(TimeSpan))
            {
                return reader.ReadTimeSpan();
            }
            if(formalType == typeof(float))
            {
                return reader.ReadSingle();
            }
            if(formalType == typeof(double))
            {
                return reader.ReadDouble();
            }
            var returnedObject = Activator.CreateInstance(formalType);
            // here we have a boxed struct which we put to struct reference list
            UpdateFields(formalType, returnedObject);
            // if this is subtype
            return returnedObject;
        }

        private void FillList(Type elementFormalType, IList list)
        {
            var count = reader.ReadInt32();
            for(var i = 0; i < count; i++)
            {
                var element = ReadField(elementFormalType);
                list.Add(element);
            }
        }

        private void FillCollection(Type elementFormalType, object obj)
        {
            var collectionType = obj.GetType();
            var count = reader.ReadInt32();
            var addMethod = collectionType.GetMethod("Add", new [] { elementFormalType }) ??
				collectionType.GetMethod("Enqueue", new [] { elementFormalType });
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
                delegateType = typeof(Func<,>).MakeGenericType(new [] { elementFormalType, addMethod.ReturnType });
            }
            var addDelegate = Delegate.CreateDelegate(delegateType, obj, addMethod);
            for(var i = 0; i < count; i++)
            {
                var fieldValue = ReadField(elementFormalType);
                addDelegate.FastDynamicInvoke(fieldValue);
            }
        }

        private void FillDictionary(Type formalKeyType, Type formalValueType, object obj)
        {
            var dictionaryType = obj.GetType();
            var count = reader.ReadInt32();
            var addMethodArgumentTypes = new [] { formalKeyType, formalValueType };
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
                delegateType = typeof(Func<,,>).MakeGenericType(new [] { formalKeyType, formalValueType, addMethod.ReturnType });
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

        private object TouchObject(int typeId, int refId)
        {
            if(typeId > typeArray.Length)
            {
                throw new ArgumentException("Argument typeId out of range");
            }
            if(deserializedObjects[refId] != null)
            {
                return deserializedObjects[refId];
            }
            UpdateMaximumReferenceId(refId);
            var actualType = typeArray[typeId];
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

        private void UpdateMaximumReferenceId(int value)
        {
            objectsCreated = Math.Max(objectsCreated, value);
        }

        private static CreationWay GetCreationWay(Type actualType)
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

        private static IEnumerable<FieldInfo> GetFieldsToSerialize(Type actualType)
        {
            var fields = actualType.GetAllFields().Where(x => !x.Attributes.HasFlag(FieldAttributes.Literal))
                .OrderBy(x => x.Name);
            return fields;
        }

        private int nextObjectToRead;
        private int objectsCreated;
        private AutoResizingList<object> deserializedObjects;
        private PrimitiveReader reader;
        private HashSet<int> inlineRead;
        private readonly Type[] typeArray;
        private readonly Stream stream;
        private readonly Action<object> postDeserializationCallback;
        private const int InitialCapacity = 128;
        private const string InternalErrorMessage = "Internal error: should not reach here.";
        private const string CouldNotFindAddErrorMessage = "Could not find suitable Add method for the type {0}.";

        private enum CreationWay
        {
            Uninitialized,
            DefaultCtor,
            Null
        }
    }
}

