/*
  Copyright (c) 2012 - 2016 Antmicro <www.antmicro.com>

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
using System.Collections;
using Migrantoid.Hooks;
using Migrantoid.Generators;
using System.Threading;
using Migrantoid.VersionTolerance;
using Migrantoid.Utilities;
using Migrantoid.Customization;
using System.Text;
using System.Reflection;

namespace Migrantoid
{
    internal class ObjectWriter
    {
        public ObjectWriter(Stream stream, Serializer.WriteMethods writeMethods, Action<object> preSerializationCallback = null,
                            Action<object> postSerializationCallback = null, SwapList surrogatesForObjects = null, SwapList objectsForSurrogates = null,
                            bool treatCollectionAsUserObject = false, bool useBuffering = true, bool disableStamping = false,
                            ReferencePreservation referencePreservation = ReferencePreservation.Preserve)
        {
            this.treatCollectionAsUserObject = treatCollectionAsUserObject;
            this.objectsForSurrogates = objectsForSurrogates;
            this.referencePreservation = referencePreservation;
            this.preSerializationCallback = preSerializationCallback;
            this.postSerializationCallback = postSerializationCallback;
            this.writeMethods = writeMethods;
            this.surrogatesForObjects = surrogatesForObjects ?? new SwapList();

            parentObjects = new Dictionary<object, object>();
            postSerializationHooks = new List<Action>();
            types = new IdentifiedElementsDictionary<TypeDescriptor>(this);
            Methods = new IdentifiedElementsDictionary<MethodDescriptor>(this);
            Assemblies = new IdentifiedElementsDictionary<AssemblyDescriptor>(this);
            Modules = new IdentifiedElementsDictionary<ModuleDescriptor>(this);
            writer = new PrimitiveWriter(stream, useBuffering);
            if(referencePreservation == ReferencePreservation.Preserve)
            {
                identifier = new ObjectIdentifier();
            }
            touchTypeMethod = disableStamping ? (Func<Type, int>)TouchAndWriteTypeIdWithSimpleStamp : TouchAndWriteTypeIdWithFullStamp;
            objectsWrittenInline = new HashSet<int>();
        }

        public void ReuseWithNewStream(Stream stream)
        {
            postSerializationHooks.Clear();
            types.Clear();
            Methods.Clear();
            Assemblies.Clear();
            Modules.Clear();
            writer = new PrimitiveWriter(stream, writer.IsBuffered);
            identifier.Clear();
        }

        public void WriteObject(object o)
        {
            if(o == null || Helpers.IsTransient(o.GetType()))
            {
                throw new ArgumentException("Cannot write a null object or a transient object.");
            }
            if(referencePreservation != ReferencePreservation.Preserve)
            {
                identifier = identifierContext == null ? new ObjectIdentifier() : new ObjectIdentifier(identifierContext);
                identifierContext = null;
            }

            try
            {
                var writtenBefore = identifier.Count;
                writeMethods.writeReferenceMethodsProvider.GetOrCreate(typeof(object))(this, o);

                if(writtenBefore != identifier.Count)
                {
                    var refId = identifier.GetId(o);
                    do
                    {
                        if(!objectsWrittenInline.Contains(refId))
                        {
                            var obj = identifier.GetObject(refId);
                            writer.Write(refId);
                            InvokeCallbacksAndExecute(obj, WriteObjectInner);
                        }

                        refId++;
                    }
                    while(identifier.Count > refId);
                }
            }
            finally
            {
                for(var i = identifier.Count - 1; i >= 0; i--)
                {
                    Completed(identifier.GetObject(i));
                }

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

        // It is necessary to pass `objectType` when `o` is null.
        private void CheckForNullOrTransientnessAndWriteDeferredReference (object o, Type objectFormalType = null)
        {
            if(objectFormalType != null && Helpers.IsTransient(objectFormalType))
            {
                return;
            }
            if(o == null || Helpers.IsTransient(o))
            {
                writer.Write(Consts.NullObjectId);
                return;
            }

            CheckLegalityAndWriteDeferredReference(o);
        }

        internal void CheckLegalityAndWriteDeferredReference(object o)
        {
            CheckLegality(o, parentObjects);
            WriteDeferredReference(o);
        }

        internal void WriteDeferredReference(object o)
        {
            bool isNew;
            var refId = identifier.GetId(o, out isNew);
            writer.Write(refId);
            if(isNew)
            {
                var method = writeMethods.surrogateObjectIfNeededMethodsProvider.GetOrCreate(o.GetType());
                if(method != null)
                {
                    o = method(this, o, refId);
                }
                // we should write a type reference here!
                // and some special data in case of some types, i.e. surrogates or arrays
                var type = o.GetType();
                TouchAndWriteTypeId(type);
                writeMethods.handleNewReferenceMethodsProvider.GetOrCreate(type)(this, o, refId);
            }
        }

        internal object SurrogateObjectIfNeeded(object o, int refId)
        {
            var surrogateId = surrogatesForObjects.FindMatchingIndex(o.GetType());
            if(surrogateId != -1)
            {
                o = surrogatesForObjects.GetByIndex(surrogateId).DynamicInvoke(new[] { o });
                // special case - surrogation!
                // setting identifier for new object does not remove original one from the mapping
                // thanks to that behaviour surrogation preserves identity
                identifier.SetIdentifierForObject(o, refId);
            }

            return o;
        }

        internal void HandleNewReference(object o, int refId)
        {
            var objectForSurrogatesIndex = objectsForSurrogates == null ? -1 : objectsForSurrogates.FindMatchingIndex(o.GetType());
            writer.Write(objectForSurrogatesIndex != -1);
            if(objectForSurrogatesIndex != -1)
            {
                // we use counter-surrogate here just to determine the type of final object
                // bare in mind that it does not have to be the same as initial type of an object
                var restoredObject = objectsForSurrogates.GetByIndex(objectForSurrogatesIndex).DynamicInvoke(new[] { o });
                TouchAndWriteTypeId(restoredObject.GetType());
            }
            if(TryWriteObjectInline(o))
            {
                objectsWrittenInline.Add(refId);
            }
        }

        internal bool TryWriteObjectInline(object o)
        {
            var type = o.GetType();
            if(type.IsArray)
            {
                WriteArrayMetadata((Array)o);
                return false;
            }
            if(type == typeof(string))
            {
                InvokeCallbacksAndExecute(o, s => writer.Write((string)s));
                return true;
            }
            return WriteSpecialObject(o, false);
        }

        internal Delegate[] GetDelegatesWithNonTransientTargets(MulticastDelegate mDelegate)
        {
            return mDelegate.GetInvocationList().Where(x => x.Target == null || !Helpers.IsTransient(x.Target)).ToArray();
        }

        internal static void CheckLegality(Type type)
        {
            if(IsTypeIllegal(type))
            {
                throw new InvalidOperationException("Pointer or ThreadLocal or SpinLock encountered during serialization. In order to obtain detailed information including classes path that lead here, please use generated version of serializer.");
            }
        }

        internal static void CheckLegality(object obj, Dictionary<object, object> parents)
        {
            if(obj == null)
            {
                return;
            }
            var type = obj.GetType();
            // containing type is a hint in case of
            if(IsTypeIllegal(type))
            {
                var path = new StringBuilder();
                var current = obj;
                while(parents.ContainsKey(current))
                {
                    path.Insert(0, " => ");
                    path.Insert(0, current.GetType().Name);
                    current = parents[current];
                }
                path.Insert(0, " => ");
                path.Insert(0, current.GetType().Name);

                throw new InvalidOperationException("Pointer or ThreadLocal or SpinLock encountered during serialization. The classes path that lead to it was: " + path);
            }
        }

        private static bool IsTypeIllegal(Type type)
        {
            return type.IsPointer || type == typeof(IntPtr) || type == typeof(Pointer) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ThreadLocal<>)) || type == typeof(SpinLock);
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

                var typeOfUnderlyingType = Helpers.GetTypeOfGenericType(typeDescriptor.UnderlyingType);
                if(typeOfUnderlyingType == Helpers.TypeOfGenericType.OpenGenericType)
                {
                    writer.Write(false);
                }
                else if(typeOfUnderlyingType == Helpers.TypeOfGenericType.ClosedGenericType || typeOfUnderlyingType == Helpers.TypeOfGenericType.FixedNestedGenericType)
                {
                    writer.Write(true);
                    foreach(var genericArgumentType in typeDescriptor.UnderlyingType.GetGenericArguments())
                    {
                        TouchAndWriteTypeIdWithFullStamp(genericArgumentType);
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format("Unexpected generic type: {0}", typeDescriptor.UnderlyingType));
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
            objectsWrittenInline.Clear();
            parentObjects.Clear();

            if(referencePreservation == ReferencePreservation.UseWeakReference)
            {
                identifierContext = identifier.GetContext();
            }
            if(referencePreservation != ReferencePreservation.Preserve)
            {
                identifier = null;
            }
        }

        private void InvokeCallbacksAndExecute(object o, Action<object> action)
        {
            try
            {
                if(preSerializationCallback != null)
                {
                    preSerializationCallback(o);
                }
                action(o);
            }
            finally
            {
                if(postSerializationCallback != null)
                {
                    postSerializationCallback(o);
                }
            }
        }

        private void WriteObjectInner(object o)
        {
            writeMethods.writeMethodsProvider.GetOrCreate(o.GetType())(this, o);
        }

        private void WriteObjectsFields(object o, Type type)
        {
            // fields in the alphabetical order
            var fields = StampHelpers.GetFieldsInSerializationOrder(type);
            foreach(var field in fields)
            {
                var formalType = field.FieldType;
                var value = field.GetValue(o);
                if(value != null)
                {
                    parentObjects[value] = o;
                }

                if(Helpers.IsTypeWritableDirectly(formalType))
                {
                    WriteValueType(formalType, value);
                }
                else
                {
                    CheckForNullOrTransientnessAndWriteDeferredReference(value, formalType);
                }
            }
        }

        private bool WriteSpecialObject(object o, bool checkForCollections)
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
            if(type.IsArray)
            {
                var elementType = type.GetElementType();
                var array = o as Array;
                WriteArray(elementType, array);
                return true;
            }

            if(checkForCollections)
            {
                CollectionMetaToken collectionToken;
                if (CollectionMetaToken.TryGetCollectionMetaToken(o.GetType(), out collectionToken))
                {
                    // here we can have normal or extension method that needs to be treated differently
                    int count = collectionToken.CountMethod.IsStatic ?
                                (int)collectionToken.CountMethod.Invoke(null, new[] { o }) :
                                (int)collectionToken.CountMethod.Invoke(o, null);

                    WriteEnumerable(collectionToken.FormalElementType, count, (IEnumerable)o);
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

        private void WriteArrayMetadata(Array array)
        {
            var rank = array.Rank;
            writer.Write(rank);
            for(var i = 0; i < rank; i++)
            {
                writer.Write(array.GetLength(i));
            }
        }

        private void WriteArray(Type elementFormalType, Array array)
        {
            var position = new int[array.Rank];
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
                break;
            case SerializationType.Value:
                WriteValueType(formalType, value);
                break;
            case SerializationType.Reference:
                CheckForNullOrTransientnessAndWriteDeferredReference(value, formalType);
                break;
            }
        }

        private void WriteValueType(Type formalType, object value)
        {
            CheckLegality(value, parentObjects);
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

        internal static WriteMethodDelegate LinkSpecialWrite(Type actualType)
        {
            if(actualType == typeof(string))
            {
                return (ow, obj) =>
                {
                    ow.writer.Write((string)obj);
                };
            }
            if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
            {
                return (ow, obj) =>
                {
                    var startingPosition = ow.writer.Position;
                    ((ISpeciallySerializable)obj).Save(ow.writer);
                    ow.writer.Write(ow.writer.Position - startingPosition);
                };
            }
            if(actualType == typeof(byte[]))
            {
                return (ow, objToWrite) =>
                {
                    var array = (byte[])objToWrite;
                    ow.writer.Write(array);
                };
            }
            return null;
        }

        /// <summary>
        /// Writes the object using reflection.
        ///
        /// REMARK: this method is not thread-safe!
        /// </summary>
        /// <param name="objectWriter">Object writer's object</param>
        /// <param name="o">Object to serialize</param>
        internal static void WriteObjectUsingReflection(ObjectWriter objectWriter, object o)
        {
            Helpers.InvokeAttribute(typeof(PreSerializationAttribute), o);
            if(!objectWriter.WriteSpecialObject(o, !objectWriter.treatCollectionAsUserObject))
            {
                objectWriter.WriteObjectsFields(o, o.GetType());
            }
        }

        private void Completed(object o)
        {
            var method = writeMethods.callPostSerializationHooksMethodsProvider.GetOrCreate(o.GetType());
            if(method != null)
            {
                method(this, o);
            }
        }

        internal void CallPostSerializationHooksUsingReflection(object o)
        {
            Helpers.InvokeAttribute(typeof(PostSerializationAttribute), o);
            var postHook = Helpers.GetDelegateWithAttribute(typeof(LatePostSerializationAttribute), o);
            if(postHook != null)
            {
                postSerializationHooks.Add(postHook);
            }
        }

        internal bool TreatCollectionAsUserObject { get { return treatCollectionAsUserObject; } }
        internal PrimitiveWriter PrimitiveWriter { get { return writer; } }
        internal IdentifiedElementsDictionary<ModuleDescriptor> Modules { get; private set; }
        internal IdentifiedElementsDictionary<AssemblyDescriptor> Assemblies { get; private set; }
        internal IdentifiedElementsDictionary<MethodDescriptor> Methods { get; private set; }

        internal ObjectIdentifier identifier;
        internal PrimitiveWriter writer;
        internal readonly Action<object> preSerializationCallback;
        internal readonly Action<object> postSerializationCallback;
        internal readonly List<Action> postSerializationHooks;
        internal readonly SwapList surrogatesForObjects;
        internal readonly SwapList objectsForSurrogates;
        internal readonly HashSet<int> objectsWrittenInline;

        private IdentifiedElementsDictionary<TypeDescriptor> types;
        private ObjectIdentifierContext identifierContext;
        private readonly Func<Type, int> touchTypeMethod;
        private readonly bool treatCollectionAsUserObject;
        private readonly ReferencePreservation referencePreservation;
        private readonly Dictionary<object, object> parentObjects;
        private readonly Serializer.WriteMethods writeMethods;
    }

    internal delegate void WriteMethodDelegate(ObjectWriter writer, object obj);
    internal delegate object SurrogateObjectIfNeededDelegate(ObjectWriter writer, object obj, int referenceId);
    internal delegate void HandleNewReferenceMethodDelegate(ObjectWriter writer, object obj, int referenceId);
    internal delegate void WriteReferenceMethodDelegate(ObjectWriter writer, object obj);

    internal delegate void CallPostSerializationHooksMethodDelegate(ObjectWriter writer, object o);
}

