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
    public class ObjectWriter
    {
		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.ObjectWriter" /> class.
		/// </summary>
		/// <param name='stream'>
		/// Stream to which data will be written.
		/// </param>
		/// <param name='typeIndices'>
		/// Dictionary which is used to map given type to (unique) ID. If the <see cref="strictTypes" /> is <c>true</c>,
		/// then this dictionary must contain all the types of the serialized objects.
		/// </param>
		/// <param name='strictTypes'>
		/// If this value is true, the <see cref="typeIndices" /> must contain all the types of the serialized objects,
		/// otherwise exception is thrown. When false and given type is not present in dictionary, the <see cref="missingTypeCallback" />
		/// is invoked.
		/// </param>
		/// <param name='missingTypeCallback'>
		/// Callback which is called when <see cref="strictTypes"/>  is true and type of the object to serialize cannot be found
		/// in the <see cref="typeIndices"/> dictionary. The missing type is given in its only parameter. The callback should
		/// supplement the dictionary with the missing type. Can be null if <see cref="strictTypes" /> is true.
		/// </param>
		/// <param name='preSerializationCallback'>
		/// Callback which is called once on every unique object before its serialization. Contains this object in its only parameter.
		/// </param>
		/// <param name='postSerializationCallback'>
		/// Callback which is called once on every unique object after its serialization. Contains this object in its only parameter.
		/// </param>
        public ObjectWriter(Stream stream, IDictionary<Type, int> typeIndices, bool strictTypes, Action<Type> missingTypeCallback = null,
                            Action<object> preSerializationCallback = null, Action<object> postSerializationCallback = null)
        {
            this.typeIndices = typeIndices;
            this.stream = stream;
            this.strictTypes = strictTypes;
            if(!strictTypes && missingTypeCallback == null)
            {
                throw new ArgumentNullException("The parameter missingTypeCallback cannot be null when strict types is not enabled.");
            }
            this.missingTypeCallback = missingTypeCallback;
            this.preSerializationCallback = preSerializationCallback;
            this.postSerializationCallback = postSerializationCallback;
            PrepareForNextWrite();
        }

		/// <summary>
		/// Writes the given object along with ones referenced by it.
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
                    WriteObjectInner(identifier.GetObject(objectsWritten)); // TODO: indexer maybe?
                }
                objectsWritten++;
            }
            PrepareForNextWrite();
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

        private void WriteObjectInner(object o)
        {
            if(preSerializationCallback != null)
            {
                preSerializationCallback(o);
            }
            Helpers.InvokeAttribute(typeof(PreSerializationAttribute), o);
            var type = o.GetType();
            // the actual type
            TouchType(type);
            writer.Write(typeIndices[type]);
            if(!WriteSpecialObject(o))
            {
                WriteObjectsFields(o, type);
            }
            Helpers.InvokeAttribute(typeof(PostSerializationAttribute), o);
            if(postSerializationCallback != null)
            {
                postSerializationCallback(o);
            }
        }

        private void WriteObjectsFields(object o, Type type)
        {
            // fields in the alphabetical order
            var fields = type.GetAllFields().Where(x => !x.Attributes.HasFlag(FieldAttributes.Literal) && !x.IsTransient()).OrderBy(x => x.Name);
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
            writer.Write(count);
            foreach(var element in collection)
            {
                WriteField(elementFormalType, element);
            }
        }

        private void WriteDictionary(Type formalKeyType, Type formalValueType, int count, IEnumerable dictionary)
        {
            writer.Write(count);
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
            if(formalType.IsDefined(typeof(TransientAttribute), false))
            {
                return;
            }
            if(formalType.IsValueType)
            {
                WriteValueType(formalType, value);
                return;
            }
            // OK, so we should write a reference
            // a null reference maybe?
            if(value == null)
            {
                writer.Write(Consts.NullObjectId);
                return;
            }
            var actualType = value.GetType();
            TouchType(actualType);
            writer.Write(typeIndices[actualType]);
            if(actualType.IsDefined(typeof(TransientAttribute), false))
            {
                return;
            }
            var refId = identifier.GetId(value);
            // if this is future reference, just after the reference id,
            // we should write inline data
            writer.Write(refId);
            if(Helpers.CanBeCreatedWithDataOnly(actualType) && refId > objectsWritten && !inlineWritten.Contains(refId))
            {
                inlineWritten.Add(refId);
                WriteObjectInner(value);
            }
        }

        private void WriteValueType(Type formalType, object value)
        {
            // value type -> actual type is the formal type
            if(formalType.IsEnum)
            {
                writer.Write(Impromptu.InvokeConvert(value, typeof(long), true));
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
            if(formalType == typeof(Int64))
            {
                writer.Write((Int64)value);
                return;
            }
            if(formalType == typeof(UInt64))
            {
                writer.Write((UInt64)value);
                return;
            }
            if(formalType == typeof(Int32))
            {
                writer.Write((Int32)value);
                return;
            }
            if(formalType == typeof(UInt32))
            {
                writer.Write((UInt32)value);
                return;
            }
            if(formalType == typeof(Int16))
            {
                writer.Write((Int16)value);
                return;
            }
            if(formalType == typeof(UInt16))
            {
                writer.Write((UInt16)value);
                return;
            }
            if(formalType == typeof(char))
            {
                writer.Write((char)value);
                return;
            }
            if(formalType == typeof(byte))
            {
                writer.Write((byte)value);
                return;
            }
            if(formalType == typeof(bool))
            {
                writer.Write((bool)value);
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
            if(formalType == typeof(float))
            {
                writer.Write((float)value);
                return;
            }
            if(formalType == typeof(double))
            {
                writer.Write((double)value);
                return;
            }
            // so we guess it is struct
            WriteObjectsFields(value, formalType);
        }

        private void TouchType(Type type)
        {
            if(typeIndices.ContainsKey(type))
            {
                return;
            }
            if(!strictTypes)
            {
                missingTypeCallback(type);
            }
            else
            {
                throw new InvalidOperationException(string.Format(
                    "Unexpected type encountered in the strict type mode: {0}.", type.Name));
            }
        }

        // TODO: dispose and so on
        private int objectsWritten;
        private ObjectIdentifier identifier;
        private PrimitiveWriter writer;
        private HashSet<int> inlineWritten;
        private readonly Stream stream;
        private readonly bool strictTypes;
        private readonly Action<Type> missingTypeCallback;
        private readonly Action<object> preSerializationCallback;
        private readonly Action<object> postSerializationCallback;
        private readonly IDictionary<Type, int> typeIndices;
    }
}

