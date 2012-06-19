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
using AntMicro.Migrant.Generators;

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
		/// Dictionary which is used to map given type to (unique) ID. Types in this dictionary are considered to be known upfront,
		/// i.e. their type information is not written to the serialization stream. The <see cref="AntMicro.Migrant.ObjectReader" />
		/// that will read such stream must receive consistent type set.
		/// </param>
		/// <param name='preSerializationCallback'>
		/// Callback which is called once on every unique object before its serialization. Contains this object in its only parameter.
		/// </param>
		/// <param name='postSerializationCallback'>
		/// Callback which is called once on every unique object after its serialization. Contains this object in its only parameter.
		/// </param>
        public ObjectWriter(Stream stream, IList<Type> upfrontKnownTypes, Action<object> preSerializationCallback = null, 
		                    Action<object> postSerializationCallback = null, IDictionary<Type, MethodInfo> writeMethodCache = null,
		                    bool isGenerating = true)
        {
			transientTypes = new Dictionary<Type, bool>();
			writeMethods = new Action<PrimitiveWriter, object>[0];
			this.writeMethodCache = writeMethodCache;
			this.isGenerating = isGenerating;
			TypeIndices = new Dictionary<Type, int>();
			RegenerateWriteMethods();
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

		protected internal void WriteObjectInner(object o)
		{
			var type = o.GetType();
            var typeId = TouchAndWriteTypeId(type);
			writeMethods[typeId](Writer, o);
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

		protected internal static void CheckLegality(Type type)
		{
			if(type.IsPointer || type == typeof(IntPtr))
			{
				throw new ArgumentException(); // TODO
			}
		}

        private void WriteValueType(Type formalType, object value)
        {
			CheckLegality(formalType);
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
			TypeIndices.Add(type, nextTypeId++);
			RegenerateWriteMethods();
		}

		private void RegenerateWriteMethods()
		{
			var newWriteMethods = new Action<PrimitiveWriter, object>[TypeIndices.Count];
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
						if(writeMethodCache != null && writeMethodCache.ContainsKey(entry.Key))
						{
							newWriteMethods[entry.Value] = (Action<PrimitiveWriter, object>)
								Delegate.CreateDelegate(typeof(Action<PrimitiveWriter, object>), this, writeMethodCache[entry.Key]);
						}
						else
						{
							newWriteMethods[entry.Value] = PrepareWriteMethod(entry.Key);
						}
					}
					// for transient class the delegate will never be called
				}
			}
			writeMethods = newWriteMethods;
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

			var method = new WriteMethodGenerator(actualType, TypeIndices).Method;
			var result = (Action<PrimitiveWriter, object>)method.CreateDelegate(typeof(Action<PrimitiveWriter, object>), this);
			if(writeMethodCache != null)
			{
				writeMethodCache.Add(actualType, result.Method);
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
					Console.WriteLine (this);
					var startingPosition = writer.Position;
	                ((ISpeciallySerializable)obj).Save(writer);
	                writer.Write(writer.Position - startingPosition);
				};
			}
			return null;
		}

		private readonly bool isGenerating;

		// TODO: actually, this field can be considered static
		private readonly Dictionary<Type, bool> transientTypes;
		private readonly IDictionary<Type, MethodInfo> writeMethodCache;
		private Action<PrimitiveWriter, object>[] writeMethods;

        private int objectsWritten;
		private int nextTypeId;
        protected internal ObjectIdentifier Identifier;
        protected PrimitiveWriter Writer;
        protected HashSet<int> InlineWritten;
        private readonly Stream stream;
        private readonly Action<object> preSerializationCallback;
        private readonly Action<object> postSerializationCallback;
        protected readonly Dictionary<Type, int> TypeIndices;
    }
}

