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
using System.Runtime.CompilerServices;

namespace Antmicro.Migrant
{
    /// <summary>
    /// Writes the object in a format that can be later read by <see cref="Antmicro.Migrant.ObjectReader"/>.
    /// </summary>
    public class ObjectWriter : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.ObjectWriter" /> class.
        /// </summary>
        /// <param name='stream'>
        /// Stream to which data will be written.
        /// </param>
        /// <param name='preSerializationCallback'>
        /// Callback which is called once on every unique object before its serialization. Contains this object in its only parameter.
        /// </param>
        /// <param name='postSerializationCallback'>
        /// Callback which is called once on every unique object after its serialization. Contains this object in its only parameter.
        /// </param>
        /// <param name='writeMethodCache'>
        /// Cache in which generated write methods are stored and reused between instances of <see cref="Antmicro.Migrant.ObjectWriter" />.
        /// Can be null if one does not want to use the cache. Note for the life of the cache you always have to provide the same
        /// <paramref name="surrogatesForObjects"/>.
        /// </param>
        /// <param name='surrogatesForObjects'>
        /// Dictionary, containing callbacks that provide surrogate for given type. Callbacks have to be of type Func&lt;T, object&gt; where
        /// typeof(T) is given type. Note that the list always have to be in sync with <paramref name="writeMethodCache"/>.
        /// </param>			
        /// <param name='isGenerating'>
        /// True if write methods are to be generated, false if one wants to use reflection.
        /// </param>
        /// <param name = "treatCollectionAsUserObject">
        /// True if collection objects are to be serialized without optimization (treated as normal user objects).
        /// </param>
        /// <param name="useBuffering"> 
        /// True if buffering is used. False if all writes should directly go to the stream and no padding should be used.
        /// </param>
        /// <param name="referencePreservation"> 
        /// Tells serializer how to treat object identity between the calls to <see cref="Antmicro.Migrant.ObjectWriter.WriteObject" />.
        /// </param>
        public ObjectWriter(Stream stream, Action<object> preSerializationCallback = null, 
                      Action<object> postSerializationCallback = null, IDictionary<Type, DynamicMethod> writeMethodCache = null,
                      InheritanceAwareList<Delegate> surrogatesForObjects = null, bool isGenerating = true, bool treatCollectionAsUserObject = false,
                      bool useBuffering = true, ReferencePreservation referencePreservation = ReferencePreservation.Preserve)
        {
            if(surrogatesForObjects == null)
            {
                surrogatesForObjects = new InheritanceAwareList<Delegate>();
            }
            currentlyWrittenTypes = new Stack<Type>();
            transientTypeCache = new Dictionary<Type, bool>();
            writeMethods = new Dictionary<Type, Action<PrimitiveWriter, object>>();
            postSerializationHooks = new List<Action>();
            this.writeMethodCache = writeMethodCache;
            this.isGenerating = isGenerating;
            this.treatCollectionAsUserObject = treatCollectionAsUserObject;
            this.surrogatesForObjects = surrogatesForObjects;
            typeIndices = new Dictionary<TypeDescriptor, int>();
            methodIndices = new Dictionary<MethodInfo, int>();
            assemblyIndices = new Dictionary<AssemblyDescriptor, int>();
            this.preSerializationCallback = preSerializationCallback;
            this.postSerializationCallback = postSerializationCallback;
            writer = new PrimitiveWriter(stream, useBuffering);
            inlineWritten = new HashSet<int>();
            this.referencePreservation = referencePreservation;
            if(referencePreservation == ReferencePreservation.Preserve)
            {
                identifier = new ObjectIdentifier();
            }
        }

        public bool TreatCollectionAsUserObject { get { return treatCollectionAsUserObject; } }

        public PrimitiveWriter PrimitiveWriter { get { return writer; } }

        /// <summary>
        /// Writes the given object along with the ones referenced by it.
        /// </summary>
        /// <param name='o'>
        /// The object to write.
        /// </param>
        public void WriteObject(object o)
        {
            if(o == null || Helpers.CheckTransientNoCache(o.GetType()))
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

        /// <summary>
        /// Releases all resource used by the <see cref="Antmicro.Migrant.ObjectWriter"/> object. Note that this is not necessary
        /// if buffering is not used.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Antmicro.Migrant.ObjectWriter"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Antmicro.Migrant.ObjectWriter"/> in an unusable state.
        /// After calling <see cref="Dispose"/>, you must release all references to the
        /// <see cref="Antmicro.Migrant.ObjectWriter"/> so the garbage collector can reclaim the memory that the
        /// <see cref="Antmicro.Migrant.ObjectWriter"/> was occupying.</remarks>
        public void Dispose()
        {
            writer.Dispose();
            writer = null;
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

        internal static void CheckLegality(Type type, Type containingType = null, IEnumerable<Type> writtenTypes = null)
        {
            // containing type is a hint in case of 
            if(type.IsPointer || type == typeof(IntPtr) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ThreadLocal<>)))
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
                throw new InvalidOperationException("Pointer or ThreadLocal encountered during serialization. The classes path that lead to it was: " + path);
            }
        }

        internal int TouchAndWriteMethodId(MethodInfo info)
        {
            int methodId;
            if(methodIndices.ContainsKey(info))
            {
                methodId = methodIndices[info];
                writer.Write(methodId);
                return methodId;
            }
            AddMissingMethod(info);
            methodId = methodIndices[info];
            writer.Write(methodId);
            TouchAndWriteTypeId(info.ReflectedType);
            writer.Write(info.Name);

            var parameters = info.GetParameters();
            writer.Write(parameters.Length);
            foreach(var p in parameters)
            {
                TouchAndWriteTypeId(p.ParameterType);
            }

            return methodId;
        }

        internal static bool HasSpecialWriteMethod(Type type)
        {
            return type == typeof(string) || typeof(ISpeciallySerializable).IsAssignableFrom(type) || Helpers.CheckTransientNoCache(type);
        }

        internal int TouchAndWriteTypeId(Type type)
        {
            var typeDescriptor = TypeDescriptor.CreateFromType(type);

            int typeId;
            if(typeIndices.ContainsKey(typeDescriptor))
            {
                typeId = typeIndices[typeDescriptor];
                writer.Write(typeId);
                return typeId;
            }
            typeId = nextTypeId++;
            typeIndices.Add(typeDescriptor, typeId);
            writer.Write(typeId);
            typeDescriptor.WriteTypeStamp(this);
            typeDescriptor.WriteStructureStampIfNeeded(this);
            return typeId;
        }

        internal int TouchAndWriteAssemblyId(AssemblyDescriptor assembly)
        {
            int assemblyId;
            if(assemblyIndices.ContainsKey(assembly))
            {
                assemblyId = assemblyIndices[assembly];
                writer.Write(assemblyId);
                return assemblyId;
            }
            assemblyId = nextAssemblyId++;
            assemblyIndices.Add(assembly, assemblyId);
            writer.Write(assemblyId);
            assembly.WriteTo(this);
            return assemblyId;
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
            var type = o.GetType();
            if(writeMethods.ContainsKey(type))
            {
                writeMethods[type](writer, o);
                return;
            }

            // we have to stamp the type if it is not surrogated
            var surrogateId = Helpers.GetSurrogateFactoryIdForType(type, surrogatesForObjects);
            if(surrogateId == -1)
            {
                TouchAndWriteTypeId(type);
                typeIdJustWritten = true;
            }

            Action<PrimitiveWriter, object> writeMethod;
            if(writeMethodCache != null && writeMethodCache.ContainsKey(type))
            {
                writeMethod = (Action<PrimitiveWriter, object>)writeMethodCache[type].CreateDelegate(typeof(Action<PrimitiveWriter, object>), this);
            }
            else
            {
                writeMethod = PrepareWriteMethod(type, surrogateId);
            }
            writeMethods.Add(type, writeMethod);
            writeMethod(writer, o);
        }

        private void WriteObjectUsingReflection(PrimitiveWriter primitiveWriter, object o, int typeId)
        {
            WriteTypeIdIfNecessary(typeId);
            // the primitiveWriter parameter is not used here in fact, it is only to have
            // signature compatible with the generated method
            Helpers.InvokeAttribute(typeof(PreSerializationAttribute), o);
            try
            {
                var type = o.GetType();
                currentlyWrittenTypes.Push(type);
                if(!WriteSpecialObject(o))
                {
                    WriteObjectsFields(o, type);
                }
                currentlyWrittenTypes.Pop();
            }
            finally
            {
                Helpers.InvokeAttribute(typeof(PostSerializationAttribute), o);
                var postHook = Helpers.GetDelegateWithAttribute(typeof(LatePostSerializationAttribute), o);
                if(postHook != null)
                {
                    postSerializationHooks.Add(postHook);
                }
            }
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
                    TouchAndWriteMethodId(del.Method);
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

        private void AddMissingMethod(MethodInfo info)
        {
            methodIndices.Add(info, nextMethodId++);
        }

        private Action<PrimitiveWriter, object> PrepareWriteMethod(Type actualType, int surrogateId)
        {
            var typeId = -1;
            if(surrogateId == -1)
            {
                typeId = typeIndices[TypeDescriptor.CreateFromType(actualType)];
                var specialWrite = LinkSpecialWrite(actualType, typeId);
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
                    return (pw, o) => InvokeCallbacksAndWriteObject(surrogatesForObjects.GetByIndex(surrogateId).DynamicInvoke(new [] { o }));
                }
                return (pw, o) => WriteObjectUsingReflection(pw, o, typeId);
            }

            var method = new WriteMethodGenerator(actualType, treatCollectionAsUserObject, surrogateId,
                Helpers.GetFieldInfo<ObjectWriter, Dictionary<TypeDescriptor, int>>(x => x.typeIndices),
                Helpers.GetFieldInfo<ObjectWriter, InheritanceAwareList<Delegate>>(x => x.surrogatesForObjects),
                Helpers.GetFieldInfo<ObjectWriter, bool>(x => x.typeIdJustWritten),
                Helpers.GetMethodInfo<ObjectWriter>(x => x.InvokeCallbacksAndWriteObject(null))).Method;
            var result = (Action<PrimitiveWriter, object>)method.CreateDelegate(typeof(Action<PrimitiveWriter, object>), this);
            if(writeMethodCache != null)
            {
                writeMethodCache.Add(actualType, method);
            }
            return result;
        }

        private Action<PrimitiveWriter, object> LinkSpecialWrite(Type actualType, int typeId)
        {
            if(actualType == typeof(string))
            {
                return (y, obj) =>
                {
                    WriteTypeIdIfNecessary(typeId);
                    y.Write((string)obj);
                };
            }
            if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
            {
                return (writer, obj) =>
                {
                    WriteTypeIdIfNecessary(typeId);
                    var startingPosition = writer.Position;
                    ((ISpeciallySerializable)obj).Save(writer);
                    writer.Write(writer.Position - startingPosition);
                };
            }
            if(actualType == typeof(byte[]))
            {
                return (writer, objToWrite) =>
                {
                    WriteTypeIdIfNecessary(typeId);
                    writer.Write(1); // rank of the array
                    var array = (byte[])objToWrite;
                    writer.Write(array.Length);
                    writer.Write(array);
                };
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteTypeIdIfNecessary(int typeId)
        {
            if(!typeIdJustWritten)
            {
                writer.Write(typeId);
            }
            else
            {
                typeIdJustWritten = false;
            }
        }

        private ObjectIdentifier identifier;
        private ObjectIdentifierContext identifierContext;
        private int objectsWrittenThisSession;
        private int identifierCountPreviousSession;
        private int nextTypeId;
        private int nextMethodId;
        private int nextAssemblyId;
        private PrimitiveWriter writer;
        private bool typeIdJustWritten;
        private readonly HashSet<int> inlineWritten;
        private readonly bool isGenerating;
        private readonly bool treatCollectionAsUserObject;
        private readonly ReferencePreservation referencePreservation;
        private readonly Action<object> preSerializationCallback;
        private readonly Action<object> postSerializationCallback;
        private readonly List<Action> postSerializationHooks;
        private readonly Dictionary<TypeDescriptor, int> typeIndices;
        private readonly Dictionary<MethodInfo, int> methodIndices;
        private readonly Dictionary<AssemblyDescriptor, int> assemblyIndices;
        private readonly Dictionary<Type, bool> transientTypeCache;
        private readonly IDictionary<Type, DynamicMethod> writeMethodCache;
        private readonly InheritanceAwareList<Delegate> surrogatesForObjects;
        private readonly Dictionary<Type, Action<PrimitiveWriter, object>> writeMethods;
        private readonly Stack<Type> currentlyWrittenTypes;
    }
}

