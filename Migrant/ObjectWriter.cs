/*
  Copyright (c) 2012 - 2015 Antmicro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant.Generators;
using System.Reflection.Emit;
using System.Threading;
using System.Diagnostics;
using Antmicro.Migrant.VersionTolerance;
using Antmicro.Migrant.Utilities;
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant
{
    internal class ObjectWriter
    {
        public ObjectWriter(Stream stream, Action<object> preSerializationCallback = null,
                      Action<object> postSerializationCallback = null, IDictionary<Type, Action<ObjectWriter, PrimitiveWriter, object>> writeMethods = null,
                      SwapList surrogatesForObjects = null, bool isGenerating = true, bool treatCollectionAsUserObject = false,
                      bool useBuffering = true, bool disableStamping = false, ReferencePreservation referencePreservation = ReferencePreservation.Preserve)
        {
            if(surrogatesForObjects == null)
            {
                surrogatesForObjects = new SwapList();
            }
            currentlyWrittenTypes = new Stack<Type>();
            this.writeMethods = writeMethods ?? new Dictionary<Type, Action<ObjectWriter, PrimitiveWriter, object>>();
            postSerializationHooks = new List<Action>();
            this.isGenerating = isGenerating;
            this.treatCollectionAsUserObject = treatCollectionAsUserObject;
            this.surrogatesForObjects = surrogatesForObjects;
            types = new IdentifiedElementsDictionary<TypeDescriptor>(this);
            Methods = new IdentifiedElementsDictionary<MethodDescriptor>(this);
            Assemblies = new IdentifiedElementsDictionary<AssemblyDescriptor>(this);
            Modules = new IdentifiedElementsDictionary<ModuleDescriptor>(this);
            this.preSerializationCallback = preSerializationCallback;
            this.postSerializationCallback = postSerializationCallback;
            writer = new PrimitiveWriter(stream, useBuffering);
            inlineWritten = new HashSet<int>();
            this.referencePreservation = referencePreservation;
            if(referencePreservation == ReferencePreservation.Preserve)
            {
                identifier = new ObjectIdentifier();
            }

            touchTypeMethod = disableStamping ? (Func<Type, int>)TouchAndWriteTypeIdWithSimpleStamp : TouchAndWriteTypeIdWithFullStamp;
        }

        public void ReuseWithNewStream(Stream stream)
        {
            objectsWrittenThisSession = 0;
            identifierCountPreviousSession = 0;
            postSerializationHooks.Clear();
            types.Clear();
            Methods.Clear();
            Assemblies.Clear();
            Modules.Clear();
            writer = new PrimitiveWriter(stream, writer.IsBuffered);
            inlineWritten.Clear();
            identifier.Clear();
        }

        public void WriteObject(object o)
        {
            if(o == null || Helpers.IsTransient(o.GetType()))
            {
                throw new ArgumentException("Cannot write a null object or a transient object.");
            }
            objectsWrittenThisSession = 0;
            if(referencePreservation != ReferencePreservation.Preserve)
            {
                identifier = identifierContext == null ? new ObjectIdentifier() : new ObjectIdentifier(identifierContext);
                identifierContext = null;
            }
            var identifiersCount = identifier.Count;
            identifier.GetId(o);
            var firstObjectIsNew = identifiersCount != identifier.Count;

            try
            {
                // first object is always written
                InvokeCallbacksAndWriteObject(o);
                if(firstObjectIsNew)
                {
                    objectsWrittenThisSession++;
                }
                while(identifier.Count - identifierCountPreviousSession > objectsWrittenThisSession)
                {
                    if(!inlineWritten.Contains(identifierCountPreviousSession + objectsWrittenThisSession))
                    {
                        InvokeCallbacksAndWriteObject(identifier[identifierCountPreviousSession + objectsWrittenThisSession]);
                    }
                    objectsWrittenThisSession++;
                }
            }
            finally
            {
                foreach(var postHook in postSerializationHooks)
                {
                    postHook();
                }
                PrepareForNextWrite();
            }
        }

        public void Flush()
        {
            writer.Dispose();
        }

        internal void WriteObjectIdPossiblyInline(object o)
        {
            var refId = identifier.GetId(o);
            writer.Write(refId);
            if(WasNotWrittenYet(refId))
            {
                inlineWritten.Add(refId);
                InvokeCallbacksAndWriteObject(o);
            }
        }

        internal Delegate[] GetDelegatesWithNonTransientTargets(MulticastDelegate mDelegate)
        {
            return mDelegate.GetInvocationList().Where(x => x.Target == null || !Helpers.IsTransient(x.Target)).ToArray();
        }

        internal static void CheckLegality(Type type, Type containingType = null, IEnumerable<Type> writtenTypes = null)
        {
            // containing type is a hint in case of 
            if(type.IsPointer || type == typeof(IntPtr) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ThreadLocal<>)) || type == typeof(SpinLock))
            {
                IEnumerable<string> typeNames;
                if(writtenTypes == null)
                {
                    var stackTrace = new StackTrace();
                    var methods = stackTrace.GetFrames().Select(x => x.GetMethod()).ToArray();
                    var topType = containingType != null ? new [] { containingType.Name } : (new string[0]);
                    typeNames = topType.Union(
                        methods
							.Where(x => (x.Name.StartsWith("Write_") || x.Name.StartsWith("WriteArray")) && x.GetParameters()[0].ParameterType == typeof(ObjectWriter))
							.Select(x => x.Name.Substring(x.Name.IndexOf('_') + 1)));
                }
                else
                {
                    typeNames = writtenTypes.Select(x => x.Name);
                }

                var path = typeNames.Reverse().Aggregate((x, y) => x + " => " + y);
                throw new InvalidOperationException("Pointer or ThreadLocal or SpinLock encountered during serialization. The classes path that lead to it was: " + path);
            }
        }

        internal static bool HasSpecialWriteMethod(Type type)
        {
            return type == typeof(string) || typeof(ISpeciallySerializable).IsAssignableFrom(type) || Helpers.IsTransient(type);
        }

        internal int TouchAndWriteTypeId(Type type)
        {
            return touchTypeMethod(type);
        }

        private int TouchAndWriteTypeIdWithSimpleStamp(Type type)
        {
            var typeDescriptor = (TypeSimpleDescriptor)type;

            int typeId;
            if(types.Dictionary.TryGetValue(typeDescriptor, out typeId))
            {
                writer.Write(typeId);
                return typeId;
            }
            typeId = types.AddAndAdvanceId(typeDescriptor);
            writer.Write(typeId);

            typeDescriptor.Write(this);

            return typeId;
        }

        private int TouchAndWriteTypeIdWithFullStamp(Type type)
        {
            var typeDescriptor = (TypeFullDescriptor)type;
            // stamping `Type` is different than `Module`, `Assembly`, etc. so we need a special method for that
            ArrayDescriptor arrayDescriptor = null;
            if(Helpers.ContainsGenericArguments(typeDescriptor.UnderlyingType))
            {
                if(typeDescriptor.UnderlyingType.IsArray)
                {
                    arrayDescriptor = new ArrayDescriptor(typeDescriptor.UnderlyingType);
                    typeDescriptor = (TypeFullDescriptor)arrayDescriptor.ElementType;
                }
                else
                {
                    arrayDescriptor = ArrayDescriptor.EmptyRanks;
                }
            }

            if(typeDescriptor.UnderlyingType.IsGenericType)
            {
                var genericTypeDefinition = typeDescriptor.UnderlyingType.GetGenericTypeDefinition();
                var genericTypeDefinitionDescriptor = (TypeFullDescriptor)genericTypeDefinition;

                TouchAndWriteTypeIdWithFullStampInner(genericTypeDefinitionDescriptor);

                var isOpen = Helpers.IsOpenGenericType(typeDescriptor.UnderlyingType);
                writer.Write(isOpen);
                if(!isOpen)
                {
                    foreach(var genericArgumentType in typeDescriptor.UnderlyingType.GetGenericArguments())
                    {
                        TouchAndWriteTypeIdWithFullStamp(genericArgumentType);
                    }
                }
            }
            else
            {
                TouchAndWriteTypeIdWithFullStampInner(typeDescriptor);
            }

            if(arrayDescriptor != null)
            {
                writer.WriteArray(arrayDescriptor.Ranks);
            }

            return 0;
        }

        private int TouchAndWriteTypeIdWithFullStampInner(TypeDescriptor typeDescriptor)
        {
            if(typeDescriptor.UnderlyingType.IsGenericParameter)
            {
                writer.Write(typeDescriptor.UnderlyingType.GenericParameterPosition);
                writer.Write(true);
                return TouchAndWriteTypeIdWithFullStamp(typeDescriptor.UnderlyingType.DeclaringType);
            }   
            else
            {
                int typeId;
                if(types.Dictionary.TryGetValue(typeDescriptor, out typeId))
                {
                    writer.Write(typeId);
                    writer.Write(false); // generic-argument
                    return typeId;
                }
                typeId = types.AddAndAdvanceId(typeDescriptor);
                writer.Write(typeId);
                writer.Write(false); // generic-argument

                typeDescriptor.Write(this);

                return typeId;
            }
        }

        private void PrepareForNextWrite()
        {
            if(referencePreservation != ReferencePreservation.DoNotPreserve)
            {
                identifierCountPreviousSession = identifier.Count;
            }
            else
            {
                inlineWritten.Clear();
            }
            currentlyWrittenTypes.Clear();
            if(referencePreservation == ReferencePreservation.UseWeakReference)
            {
                identifierContext = identifier.GetContext();
            }
            if(referencePreservation != ReferencePreservation.Preserve)
            {
                identifier = null;
            }
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
            Action<ObjectWriter, PrimitiveWriter, object> writeMethod;
            var type = o.GetType();
            if(writeMethods.TryGetValue(type, out writeMethod))
            {
                writeMethod(this, writer, o);
                return;
            }

            var surrogateId = surrogatesForObjects.FindMatchingIndex(type);
            writeMethod = PrepareWriteMethod(type, surrogateId);

            writeMethods.Add(type, writeMethod);
            writeMethod(this, writer, o);
        }

        private void WriteObjectsFields(object o, Type type)
        {
            // fields in the alphabetical order
            var fields = StampHelpers.GetFieldsInSerializationOrder(type);
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
                    Methods.TouchAndWriteId(new MethodDescriptor(del.Method));
                }
                return true;
            }
            var str = o as string;
            if(str != null)
            {
                writer.Write(str);
                return true;
            }

            if(!treatCollectionAsUserObject)
            {
                CollectionMetaToken collectionToken; 
                if (CollectionMetaToken.TryGetCollectionMetaToken(o.GetType(), out collectionToken))
                {
                    // here we can have normal or extension method that needs to be treated differently
                    int count = collectionToken.CountMethod.IsStatic ? 
                                (int)collectionToken.CountMethod.Invoke(null, new[] { o }) : 
                                (int)collectionToken.CountMethod.Invoke(o, null); 

                    if(collectionToken.IsDictionary)
                    {
                        WriteDictionary(collectionToken, count, o);
                    }
                    else
                    {
                        WriteEnumerable(collectionToken.FormalElementType, count, (IEnumerable)o);
                    }
                    return true;
                }
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

        private void WriteDictionary(CollectionMetaToken collectionToken, int count, object dictionary)
        {
            writer.Write(count);
            if(collectionToken.IsGeneric)
            {
                var enumeratorMethod = typeof(IEnumerable<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(collectionToken.FormalKeyType, collectionToken.FormalValueType)).GetMethod("GetEnumerator");
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
                    WriteField(collectionToken.FormalKeyType, keyValue);
                    WriteField(collectionToken.FormalValueType, valueValue);
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
            if(value == null || Helpers.IsTransient(value))
            {
                writer.Write(Consts.NullObjectId);
                return;
            }
            var actualType = value.GetType();
            if(Helpers.IsTransient(actualType))
            {
                return;
            }
            CheckLegality(actualType, writtenTypes: currentlyWrittenTypes);
            var refId = identifier.GetId(value);
            // if this is a future reference, just after the reference id,
            // we should write inline data
            writer.Write(refId);
            if(WasNotWrittenYet(refId))
            {
                inlineWritten.Add(refId);
                InvokeCallbacksAndWriteObject(value);
            }
        }

        private bool WasNotWrittenYet(int referenceId)
        {
            return referenceId > (identifierCountPreviousSession + objectsWrittenThisSession) && !inlineWritten.Contains(referenceId);
        }

        private void WriteValueType(Type formalType, object value)
        {
            CheckLegality(formalType, writtenTypes: currentlyWrittenTypes);
            // value type -> actual type is the formal type
            if(formalType.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(formalType);
                var convertedValue = Convert.ChangeType(value, underlyingType);
                var method = writer.GetType().GetMethod("Write", new [] { underlyingType });
                method.Invoke(writer, new[] { convertedValue });
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
            if(Helpers.IsWriteableByPrimitiveWriter(formalType))
            {
                writer.Write((dynamic)value);
                return;
            }

            // so we guess it is struct
            WriteObjectsFields(value, formalType);
        }

        private Action<ObjectWriter, PrimitiveWriter, object> PrepareWriteMethod(Type actualType, int surrogateId)
        {
            if(surrogateId == -1)
            {
                var specialWrite = LinkSpecialWrite(actualType);
                if(specialWrite != null)
                {
                    // linked methods are not added to writeMethodCache, there's no point
                    return specialWrite;
                }
            }

            if(!isGenerating)
            {
                if(surrogateId != -1)
                {
                    return (ow, pw, o) => ow.InvokeCallbacksAndWriteObject(surrogatesForObjects.GetByIndex(surrogateId).DynamicInvoke(new [] { o }));
                }
                return WriteObjectUsingReflection;
            }

            var method = new WriteMethodGenerator(actualType, treatCollectionAsUserObject, surrogateId,
                Helpers.GetFieldInfo<ObjectWriter, SwapList>(x => x.surrogatesForObjects),
                Helpers.GetMethodInfo<ObjectWriter>(x => x.InvokeCallbacksAndWriteObject(null))).Method;
            var result = (Action<ObjectWriter, PrimitiveWriter, object>)method.CreateDelegate(typeof(Action<ObjectWriter, PrimitiveWriter, object>));
            return result;
        }

        private static Action<ObjectWriter, PrimitiveWriter, object> LinkSpecialWrite(Type actualType)
        {
            if(actualType == typeof(string))
            {
                return (ow, pw, obj) =>
                {
                    ow.TouchAndWriteTypeId(actualType);
                    pw.Write((string)obj);
                };
            }
            if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
            {
                return (ow, pw, obj) =>
                {
                    ow.TouchAndWriteTypeId(actualType);
                    var startingPosition = pw.Position;
                    ((ISpeciallySerializable)obj).Save(pw);
                    pw.Write(pw.Position - startingPosition);
                };
            }
            if(actualType == typeof(byte[]))
            {
                return (ow, pw, objToWrite) =>
                {
                    ow.TouchAndWriteTypeId(actualType);
                    pw.Write(1); // rank of the array
                    var array = (byte[])objToWrite;
                    pw.Write(array.Length);
                    pw.Write(array);
                };
            }
            return null;
        }

        private static void WriteObjectUsingReflection(ObjectWriter objectWriter, PrimitiveWriter primitiveWriter, object o)
        {
            objectWriter.TouchAndWriteTypeId(o.GetType());
            // the primitiveWriter and parameter is not used here in fact, it is only to have
            // signature compatible with the generated method
            Helpers.InvokeAttribute(typeof(PreSerializationAttribute), o);
            try
            {
                var type = o.GetType();
                objectWriter.currentlyWrittenTypes.Push(type);
                if(!objectWriter.WriteSpecialObject(o))
                {
                    objectWriter.WriteObjectsFields(o, type);
                }
                objectWriter.currentlyWrittenTypes.Pop();
            }
            finally
            {
                Helpers.InvokeAttribute(typeof(PostSerializationAttribute), o);
                var postHook = Helpers.GetDelegateWithAttribute(typeof(LatePostSerializationAttribute), o);
                if(postHook != null)
                {
                    objectWriter.postSerializationHooks.Add(postHook);
                }
            }
        }

        internal bool TreatCollectionAsUserObject { get { return treatCollectionAsUserObject; } }
        internal PrimitiveWriter PrimitiveWriter { get { return writer; } }
        internal IdentifiedElementsDictionary<ModuleDescriptor> Modules { get; private set; }
        internal IdentifiedElementsDictionary<AssemblyDescriptor> Assemblies { get; private set; }
        internal IdentifiedElementsDictionary<MethodDescriptor> Methods { get; private set; }

        private IdentifiedElementsDictionary<TypeDescriptor> types;
        private ObjectIdentifier identifier;
        private ObjectIdentifierContext identifierContext;
        private int objectsWrittenThisSession;
        private int identifierCountPreviousSession;
        private PrimitiveWriter writer;
        private readonly Func<Type, int> touchTypeMethod;
        private readonly HashSet<int> inlineWritten;
        private readonly bool isGenerating;
        private readonly bool treatCollectionAsUserObject;
        private readonly ReferencePreservation referencePreservation;
        private readonly Action<object> preSerializationCallback;
        private readonly Action<object> postSerializationCallback;
        private readonly List<Action> postSerializationHooks;
        private readonly SwapList surrogatesForObjects;
        private readonly IDictionary<Type, Action<ObjectWriter, PrimitiveWriter, object>> writeMethods;
        private readonly Stack<Type> currentlyWrittenTypes;
    }
}

