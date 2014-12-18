// *******************************************************************
//
//  Copyright (c) 2013, Antmicro Ltd
//  Author:
//    Konrad Kruczyński (kkruczynski@antmicro.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// *******************************************************************
using System.Runtime.Serialization;
using System;
using System.Reflection;

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
            var type = Type.GetType(assemblyQualifiedName);

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


