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
using ImpromptuInterface.Dynamic;

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
		/// <param name='typeIndices'>
		/// Dictionary which is used to map given type to (unique) ID.
		/// </param>
		/// <param name='missingTypeCallback'>
		/// Callback which is called when the type of the object to serialize cannot be found in the <see cref="typeIndices"/> 
		/// dictionary. The missing type is given in its only parameter. The callback should supplement the dictionary with 
		/// the missing type. Can be null, if user do not expects it to be called (for example, when all types are preinitialized).
		/// </param>
		/// <param name='preSerializationCallback'>
		/// Callback which is called once on every unique object before its serialization. Contains this object in its only parameter.
		/// </param>
		/// <param name='postSerializationCallback'>
		/// Callback which is called once on every unique object after its serialization. Contains this object in its only parameter.
		/// </param>
        public ObjectWriter(Stream stream, IDictionary<Type, int> typeIndices, Action<Type> missingTypeCallback = null,
                            Action<object> preSerializationCallback = null, Action<object> postSerializationCallback = null)
        {
            TypeIndices = typeIndices;
            this.stream = stream;
            this.missingTypeCallback = missingTypeCallback;
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
            Identifier.GetId(o);
            while(Identifier.Count > objectsWritten)
            {
                if(!InlineWritten.Contains(objectsWritten))
                {
                    InvokeCallbacksAndWriteObject(Identifier.GetObject(objectsWritten)); // TODO: indexer maybe?
                }
                objectsWritten++;
            }
            PrepareForNextWrite();
        }

        private void PrepareForNextWrite()
        {
            if(Writer != null)
            {
                Writer.Dispose();
            }
            Identifier = new ObjectIdentifier();
            Writer = new PrimitiveWriter(stream);
            InlineWritten = new HashSet<int>();
        }

        protected void InvokeCallbacksAndWriteObject(object o)
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

		protected internal virtual void WriteObjectInner(object o)
		{
			Helpers.InvokeAttribute(typeof(PreSerializationAttribute), o);
            var type = o.GetType();
            // the actual type
            TouchAndWriteTypeId(type);
            if(!WriteSpecialObject(o))
            {
                WriteObjectsFields(o, type);
            }
            Helpers.InvokeAttribute(typeof(PostSerializationAttribute), o);
		}

        private void WriteObjectsFields(object o, Type type)
        {
            // fields in the alphabetical order
            var fields = type.GetAllFields().Where(Helpers.IsNotTransient).OrderBy(x => x.Name);
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
                var startingPosition = Writer.Position;
                speciallySerializable.Save(Writer);
                Writer.Write(Writer.Position - startingPosition);
                return true;
            }
            if(type.IsArray)
            {
                var elementType = type.GetElementType();
                var array = o as Array;
                var rank = array.Rank;
                Writer.Write(rank);
                WriteArray(elementType, array);
                return true;
            }
            var str = o as string;
            if(str != null)
            {
                Writer.Write(str);
                return true;
            }
            int count;
            // dictionary has precedence before collection
            Type formalKeyType;
            Type formalValueType;
            if(Helpers.TryGetDictionaryCountAndElementTypes(o, out count, out formalKeyType, out formalValueType))
            {
                WriteDictionary(formalKeyType, formalValueType, count, (IEnumerable)o);
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
            Writer.Write(count);
            foreach(var element in collection)
            {
                WriteField(elementFormalType, element);
            }
        }

        private void WriteDictionary(Type formalKeyType, Type formalValueType, int count, IEnumerable dictionary)
        {
            Writer.Write(count);
            var keyInvocation = new CacheableInvocation(InvocationKind.Get, "Key");
            var valueInvocation = new CacheableInvocation(InvocationKind.Get, "Value");
            foreach(var element in dictionary)
            {
                WriteField(formalKeyType, keyInvocation.Invoke(element));
                WriteField(formalValueType, valueInvocation.Invoke(element));
            }
        }

        private void WriteArray(Type elementFormalType, Array array)
        {
            var rank = array.Rank;
            for(var i = 0; i < rank; i++)
            {
                Writer.Write(array.GetLength(i));
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
            if(value == null)
            {
                Writer.Write(Consts.NullObjectId);
                return;
            }
            TouchAndWriteTypeId(value);
			var actualType = value.GetType(); // TODO: optimize?
            if(actualType.IsDefined(typeof(TransientAttribute), false))
            {
                return;
            }
            var refId = Identifier.GetId(value);
            // if this is a future reference, just after the reference id,
            // we should write inline data
            Writer.Write(refId);
            if(ShouldBeInlined(actualType, refId))
            {
                InlineWritten.Add(refId);
                InvokeCallbacksAndWriteObject(value);
            }
		}
		protected internal bool ShouldBeInlined(Type type, int referenceId)
		{
			return Helpers.CanBeCreatedWithDataOnly(type) && referenceId > objectsWritten && !InlineWritten.Contains(referenceId);
		}

        private void WriteValueType(Type formalType, object value)
        {
            // value type -> actual type is the formal type
            if(formalType.IsEnum)
            {
                Writer.Write(Impromptu.InvokeConvert(value, typeof(long), true));
                return;
            }
            var nullableActualType = Nullable.GetUnderlyingType(formalType);
            if(nullableActualType != null)
            {
                if(value != null)
                {
                    Writer.Write(true);
                    WriteValueType(nullableActualType, value);
                }
                else
                {
                    Writer.Write(false);
                }
                return;
            }
            if(formalType == typeof(Int64))
            {
                Writer.Write((Int64)value);
                return;
            }
            if(formalType == typeof(UInt64))
            {
                Writer.Write((UInt64)value);
                return;
            }
            if(formalType == typeof(Int32))
            {
                Writer.Write((Int32)value);
                return;
            }
            if(formalType == typeof(UInt32))
            {
                Writer.Write((UInt32)value);
                return;
            }
            if(formalType == typeof(Int16))
            {
                Writer.Write((Int16)value);
                return;
            }
            if(formalType == typeof(UInt16))
            {
                Writer.Write((UInt16)value);
                return;
            }
            if(formalType == typeof(char))
            {
                Writer.Write((char)value);
                return;
            }
            if(formalType == typeof(byte))
            {
                Writer.Write((byte)value);
                return;
            }
            if(formalType == typeof(bool))
            {
                Writer.Write((bool)value);
                return;
            }
            if(formalType == typeof(DateTime))
            {
                Writer.Write((DateTime)value);
                return;
            }
            if(formalType == typeof(TimeSpan))
            {
                Writer.Write((TimeSpan)value);
                return;
            }
            if(formalType == typeof(float))
            {
                Writer.Write((float)value);
                return;
            }
            if(formalType == typeof(double))
            {
                Writer.Write((double)value);
                return;
            }
            // so we guess it is struct
            WriteObjectsFields(value, formalType);
        }

        protected internal int TouchAndWriteTypeId(Type type)
        {
			int typeId;
            if(TypeIndices.ContainsKey(type))
            {
				typeId = TypeIndices[type];
				Writer.Write(typeId);
                return typeId;
            }
			AddMissingType(type);
			typeId = TypeIndices[type];
			Writer.Write(typeId);
			Writer.Write(type.AssemblyQualifiedName);
			return typeId;
        }

		protected internal void TouchAndWriteTypeId(Object o)
		{
			TouchAndWriteTypeId(o.GetType());
		}

		protected virtual void AddMissingType(Type type)
		{
			missingTypeCallback(type);
		}

        private int objectsWritten;
        protected internal ObjectIdentifier Identifier;
        protected PrimitiveWriter Writer;
        protected HashSet<int> InlineWritten;
        private readonly Stream stream;
        private readonly Action<Type> missingTypeCallback;
        private readonly Action<object> preSerializationCallback;
        private readonly Action<object> postSerializationCallback;
        protected readonly IDictionary<Type, int> TypeIndices;
    }
}

