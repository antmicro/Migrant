//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.Runtime.Serialization;
using System;
using System.Reflection;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal sealed class SurrogateForISerializable
    {
        public SurrogateForISerializable(ISerializable serializable)
        {
            var serializationInfo = new SerializationInfo(serializable.GetType(), new FormatterConverter());
            var streamingContext = new StreamingContext(StreamingContextStates.Clone);
            serializable.GetObjectData(serializationInfo, streamingContext);
            keys = new string[serializationInfo.MemberCount];
            values = new object[serializationInfo.MemberCount];
            var i = 0;
            foreach(var entry in serializationInfo)
            {
                keys[i] = entry.Name;
                values[i] = entry.Value;
                i++;
            }
            assemblyQualifiedName = serializable.GetType().AssemblyQualifiedName;
        }

        public object Restore()
        {
            var type = TypeProvider.GetType(assemblyQualifiedName);

            var ctor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new [] {
                typeof(SerializationInfo),
                typeof(StreamingContext)
            }, null);

            var serializationInfo = new SerializationInfo(type, new FormatterConverter());
            serializationInfo.SetType(type);
            for(var i = 0; i < keys.Length; i++)
            {
                serializationInfo.AddValue(keys[i], values[i]);
            }
            var streamingContext = new StreamingContext(StreamingContextStates.Clone);
            var result = ctor.Invoke(new object[] { serializationInfo, streamingContext });
            var onDeserialization = result as IDeserializationCallback;
            if(onDeserialization != null)
            {
                onDeserialization.OnDeserialization(this);
            }

            return result;
        }

        private readonly string[] keys;
        private readonly object[] values;
        private readonly string assemblyQualifiedName;
    }
}


