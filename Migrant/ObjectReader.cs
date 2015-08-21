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
using System.Runtime.Serialization;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant.Utilities;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using Antmicro.Migrant.Generators;
using Antmicro.Migrant.VersionTolerance;
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant
{
    /// <summary>
    /// Reads the object previously written by <see cref="Antmicro.Migrant.ObjectWriter" />.
    /// </summary>
    public class ObjectReader : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.ObjectReader" /> class.
        /// </summary>
        /// <param name='stream'>
        /// Stream from which objects will be read.
        /// </param>
        /// <param name='objectsForSurrogates'>
        /// Dictionary, containing callbacks that provide objects for given type of surrogate. Callbacks have to be of type Func&lt;T, object&gt; where
        /// typeof(T) is type of surrogate. They always have to be in sync with <paramref name="readMethods"/>.
        /// </param>
        /// <param name='postDeserializationCallback'>
        /// Callback which will be called after deserialization of every unique object. Deserialized
        /// object is given in the callback's only parameter.
        /// </param>
        /// <param name='readMethods'>
        /// Cache in which generated read methods are stored and reused between instances of <see cref="Antmicro.Migrant.ObjectReader" />.
        /// Can be null if one does not want to use the cache. For the life of the cache you always have to provide the same 
        /// <paramref name="objectsForSurrogates"/>.
        /// </param>
        /// <param name='isGenerating'>
        /// True if read methods are to be generated, false if one wants to use reflection.
        /// </param>
        /// <param name = "treatCollectionAsUserObject">
        /// True if collection objects are to be deserialized without optimization (treated as normal user objects).
        /// </param>
        /// <param name="versionToleranceLevel"> 
        /// Describes the tolerance level of this reader when handling discrepancies in type description (new or missing fields, etc.).
        /// </param> 
        /// <param name="useBuffering"> 
        /// True if buffering was used with the corresponding ObjectWriter or false otherwise - i.e. when no padding and buffering is used.
        /// </param>
        /// <param name="referencePreservation"> 
        /// Tells deserializer whether open stream serialization preserved objects identieties between serialization. Note that this option should
        /// be consistent with what was used during serialization.
        /// </param>
        public ObjectReader(Stream stream, SwapList objectsForSurrogates = null, Action<object> postDeserializationCallback = null, 
                            IDictionary<Type, DynamicMethod> readMethods = null, bool isGenerating = false, bool treatCollectionAsUserObject = false, 
                            VersionToleranceLevel versionToleranceLevel = 0, bool useBuffering = true, 
                            ReferencePreservation referencePreservation = ReferencePreservation.Preserve)
        {
            if(objectsForSurrogates == null)
            {
                objectsForSurrogates = new SwapList();
            }
            this.objectsForSurrogates = objectsForSurrogates;
            this.readMethodsCache = readMethods ?? new Dictionary<Type, DynamicMethod>();
            this.useGeneratedDeserialization = isGenerating;
            Types = new IdentifiedElementsList<TypeDescriptor>(this);
            Methods = new IdentifiedElementsList<MethodDescriptor>(this);
            Assemblies = new IdentifiedElementsList<AssemblyDescriptor>(this);
            Modules = new IdentifiedElementsList<ModuleDescriptor>(this);
            postDeserializationHooks = new List<Action>();
            this.postDeserializationCallback = postDeserializationCallback;
            this.treatCollectionAsUserObject = treatCollectionAsUserObject;
            delegatesCache = new Dictionary<Type, Func<int, object>>();
            reader = new PrimitiveReader(stream, useBuffering);
            this.referencePreservation = referencePreservation;
            this.VersionToleranceLevel = versionToleranceLevel;
        }

        /// <summary>
        /// Reads the object with the expected formal type <typeparamref name='T'/>.
        /// </summary>
        /// <returns>
        /// The object, previously written by the <see cref="Antmicro.Migrant.ObjectWriter" />.
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
            if(soFarDeserialized != null)
            {
                deserializedObjects = new AutoResizingList<object>(soFarDeserialized.Length);
                for(var i = 0; i < soFarDeserialized.Length; i++)
                {
                    deserializedObjects[i] = soFarDeserialized[i].Target;
                }
            }
            if(deserializedObjects == null)
            {
                deserializedObjects = new AutoResizingList<object>(InitialCapacity);
            }
            var firstObjectId = deserializedObjects.Count;
            var type = Types.Read().UnderlyingType;
            if(useGeneratedDeserialization)
            {
                ReadObjectInnerGenerated(type, firstObjectId);
            }
            else
            {
                ReadObjectInner(type, firstObjectId);
            }

            var obj = deserializedObjects[firstObjectId];
            if(!(obj is T))
            {
                throw new InvalidDataException(
                    string.Format("Type {0} requested to be deserialized, however type {1} encountered in the stream.",
                        typeof(T), obj.GetType()));
            }
			
            PrepareForNextRead();
            foreach(var hook in postDeserializationHooks)
            {
                hook();
            }
            return (T)obj;
        }

        /// <summary>
        /// Releases all resource used by the <see cref="Antmicro.Migrant.ObjectReader"/> object. Note that this is not necessary
        /// if buffering is not used.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Antmicro.Migrant.ObjectReader"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Antmicro.Migrant.ObjectReader"/> in an unusable state.
        /// After calling <see cref="Dispose"/>, you must release all references to the
        /// <see cref="Antmicro.Migrant.ObjectReader"/> so the garbage collector can reclaim the memory that the
        /// <see cref="Antmicro.Migrant.ObjectReader"/> was occupying.</remarks>
        public void Dispose()
        {
            reader.Dispose();
        }

        private void TouchReadMethod(Type type)
        {
            if(!readMethodsCache.ContainsKey(type))
            {
                var surrogateId = objectsForSurrogates.FindMatchingIndex(type);
                var rmg = new ReadMethodGenerator(type, treatCollectionAsUserObject, surrogateId,
                    Helpers.GetFieldInfo<ObjectReader, SwapList>(x => x.objectsForSurrogates),
                    Helpers.GetFieldInfo<ObjectReader, AutoResizingList<object>>(x => x.deserializedObjects),
                    Helpers.GetFieldInfo<ObjectReader, PrimitiveReader>(x => x.reader),
                    Helpers.GetFieldInfo<ObjectReader, Action<object>>(x => x.postDeserializationCallback),
                    Helpers.GetFieldInfo<ObjectReader, List<Action>>(x => x.postDeserializationHooks));
                readMethodsCache.Add(type, rmg.Method);
            }
        }

        private void PrepareForNextRead()
        {
            if(referencePreservation == ReferencePreservation.UseWeakReference)
            {
                soFarDeserialized = new WeakReference[deserializedObjects.Count];
                for(var i = 0; i < soFarDeserialized.Length; i++)
                {
                    soFarDeserialized[i] = new WeakReference(deserializedObjects[i]);
                }
            }
            if(referencePreservation != ReferencePreservation.Preserve)
            {
                deserializedObjects = null;
            }
        }

        internal static bool HasSpecialReadMethod(Type type)
        {
            return type == typeof(string) || typeof(ISpeciallySerializable).IsAssignableFrom(type) || Helpers.IsTransient(type);
        }

        internal void ReadObjectInnerGenerated(Type actualType, int objectId)
        {
            TouchReadMethod(actualType);
            Func<int, object> deserializingDelegate;
            if(!delegatesCache.TryGetValue(actualType, out deserializingDelegate))
            {
                deserializingDelegate = (Func<Int32, object>)readMethodsCache[actualType].CreateDelegate(typeof(Func<Int32, object>), this);
                delegatesCache.Add(actualType, deserializingDelegate);

            }

            // execution of read method of given type
            deserializedObjects[objectId] = deserializingDelegate(objectId);
        }

        private void ReadObjectInner(Type actualType, int objectId)
        {
            TouchObject(actualType, objectId);
            switch(GetCreationWay(actualType, treatCollectionAsUserObject))
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
            var factoryId = objectsForSurrogates.FindMatchingIndex(obj.GetType());
            if(factoryId != -1)
            {
                deserializedObjects[objectId] = objectsForSurrogates.GetByIndex(factoryId).DynamicInvoke(new [] { obj });
            }
            Helpers.InvokeAttribute(typeof(PostDeserializationAttribute), obj);
            var postHook = Helpers.GetDelegateWithAttribute(typeof(LatePostDeserializationAttribute), obj);
            if(postHook != null)
            {
                if(factoryId != -1)
                {
                    throw new InvalidOperationException(string.Format(ObjectReader.LateHookAndSurrogateError, actualType));
                }
                postDeserializationHooks.Add(postHook);
            }
            if(postDeserializationCallback != null)
            {
                postDeserializationCallback(obj);
            }
        }

        private void UpdateFields(Type actualType, object target)
        {
            var fieldOrTypeInfos = ((TypeDescriptor)actualType).FieldsToDeserialize;
            foreach(var fieldOrTypeInfo in fieldOrTypeInfos)
            {
                if(fieldOrTypeInfo.Field == null)
                {
                    ReadField(fieldOrTypeInfo.TypeToOmit);
                    continue;
                }
                var field = fieldOrTypeInfo.Field;
                if(field.IsDefined(typeof(TransientAttribute), false))
                {
                    if(field.IsDefined(typeof(ConstructorAttribute), false))
                    {
                        var ctorAttribute = (ConstructorAttribute)field.GetCustomAttributes(false).First(x => x is ConstructorAttribute);

                        field.SetValue(target, Activator.CreateInstance(field.FieldType, ctorAttribute.Parameters));
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
            else if(typeof(MulticastDelegate).IsAssignableFrom(type))
            {
                ReadDelegate(type, objectId);
            }
            else if(type.IsGenericType && typeof(ReadOnlyCollection<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
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
            CollectionMetaToken token;
            if (!CollectionMetaToken.TryGetCollectionMetaToken(type, out token))
            {
                throw new InvalidOperationException(InternalErrorMessage);
            }

            if(token.IsDictionary)
            {
                FillDictionary(token, obj);
                return;
            }

            // so we can assume it is ICollection<T> or ICollection
            FillCollection(token.FormalElementType, obj);
        }

        private object ReadField(Type formalType)
        {
            if(Helpers.IsTransient(formalType))
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
                    ReadObjectInner(Types.Read().UnderlyingType, refId);
                }
                return deserializedObjects[refId];
            }
            if(formalType.IsEnum)
            {
                var value = ReadField(Enum.GetUnderlyingType(formalType));
                return Enum.ToObject(formalType, value);
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
                    addDelegate.DynamicInvoke(fieldValue);
                }
            }
        }

        private void FillDictionary(CollectionMetaToken token, object obj)
        {
            var dictionaryType = obj.GetType();
            var count = reader.ReadInt32();
            var addMethodArgumentTypes = new [] {
                token.FormalKeyType,
                token.FormalValueType
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
                    addMethodArgumentTypes[0],
                    addMethodArgumentTypes[1],
                    addMethod.ReturnType
                });
            }
            var addDelegate = Delegate.CreateDelegate(delegateType, obj, addMethod);

            for(var i = 0; i < count; i++)
            {
                var key = ReadField(addMethodArgumentTypes[0]);
                var value = ReadField(addMethodArgumentTypes[1]);
                addDelegate.DynamicInvoke(key, value);
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
                var method = Methods.Read();
                var del = Delegate.CreateDelegate(type, target, method.UnderlyingMethod);
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

        private object TouchObject(Type actualType, int refId)
        {
            if(deserializedObjects[refId] != null)
            {
                return deserializedObjects[refId];
            }

            object created = null;
            switch(GetCreationWay(actualType, treatCollectionAsUserObject))
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

        internal static CreationWay GetCreationWay(Type actualType, bool treatCollectionAsUserObject)
        {
            if(Helpers.CanBeCreatedWithDataOnly(actualType, treatCollectionAsUserObject))
            {
                return CreationWay.Null;
            }
            if(!treatCollectionAsUserObject && CollectionMetaToken.IsCollection(actualType))
            {
                return CreationWay.DefaultCtor;
            }
            if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
            {
                return CreationWay.DefaultCtor;
            }
            return CreationWay.Uninitialized;
        }

        internal bool TreatCollectionAsUserObject { get { return treatCollectionAsUserObject; } }
        internal PrimitiveReader PrimitiveReader { get { return reader; } }
        internal IdentifiedElementsList<ModuleDescriptor> Modules { get; private set; }
        internal IdentifiedElementsList<AssemblyDescriptor> Assemblies { get; private set; }
        internal IdentifiedElementsList<MethodDescriptor> Methods { get; private set; }
        internal IdentifiedElementsList<TypeDescriptor> Types { get; private set; }
        internal VersionToleranceLevel VersionToleranceLevel { get; private set; }

        internal const string LateHookAndSurrogateError = "Type {0}: late post deserialization callback cannot be used in conjunction with surrogates.";

        private WeakReference[] soFarDeserialized;
        private readonly bool useGeneratedDeserialization;
        private readonly bool treatCollectionAsUserObject;
        private ReferencePreservation referencePreservation;
        private AutoResizingList<object> deserializedObjects;
        private IDictionary<Type, DynamicMethod> readMethodsCache;
        private readonly Dictionary<Type, Func<Int32, object>> delegatesCache;
        private readonly PrimitiveReader reader;
        private readonly Action<object> postDeserializationCallback;
        private readonly List<Action> postDeserializationHooks;
        private readonly SwapList objectsForSurrogates;
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

