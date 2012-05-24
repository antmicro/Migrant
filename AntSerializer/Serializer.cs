using System;
using System.Collections.Generic;
using System.IO;

namespace AntMicro.AntSerializer
{
    public class Serializer
    {
        public Serializer()
        {
            scanner = new TypeScanner();
            typeArray = new Type[0];
            typeIndices = new Dictionary<Type, int>();
        }

        public void Initialize(Type typeToScan)
        {
            scanner.Scan(typeToScan);
            typeArray = scanner.GetTypeArray();
            UpdateTypeIndices();
        }

        public void Serialize(object obj, Stream stream, bool strictTypes = false)
        {
            if(strictTypes)
            {
                WriteTypes(stream);
            }
            var localStream = strictTypes ? stream : new MemoryStream();
            var writer = new ObjectWriter(localStream, typeIndices, strictTypes, Initialize, OnPreSerialization, OnPostSerialization);
            writer.WriteObject(obj);
            if(!strictTypes)
            {
                WriteTypes(stream);
                localStream.Seek(0, SeekOrigin.Begin);
                localStream.CopyTo(stream);
            }
        }

        public T Deserialize<T>(Stream stream)
        {
            using(var reader = new PrimitiveReader(stream))
            {
                var magic = reader.ReadUInt32();
                if(magic != Magic)
                {
                    throw new InvalidOperationException(string.Format(
                        "Cound not find proper magic {0}, instead {1} was read.", Magic, magic));
                }
                var version = reader.ReadUInt16();
                if(version != VersionNumber)
                {
                    throw new InvalidOperationException(string.Format(
                        "Could not deserialize data serialized with another version of serializer, namely {0}.", version));
                }
                var numberOfTypes = reader.ReadInt32();
                typeArray = new Type[numberOfTypes];
                for(var i = 0; i < numberOfTypes; i++)
                {
                    typeArray[i] = Type.GetType(reader.ReadString());
                }
            }
            var objectReader = new ObjectReader(stream, typeArray, OnPostDeserialization);
            var result = objectReader.ReadObject<T>();
            return result;
        }

        public event Action<object> OnPreSerialization;
        public event Action<object> OnPostSerialization;
        public event Action<object> OnPostDeserialization;

        public static T DeepClone<T>(T toClone, bool scanSourceOnly = false)
        {
            var serializer = new Serializer();
            var stream = new MemoryStream();
            if(scanSourceOnly)
            {
                serializer.Initialize(toClone.GetType());
            }
            serializer.Serialize(toClone, stream, scanSourceOnly);
            var position = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            var result = serializer.Deserialize<T>(stream);
            if(position != stream.Position)
            {
                throw new InvalidOperationException(
                    string.Format("Internal error in serializer: {0} bytes were written, but only {1} were read.",
                              position, stream.Position));
            }
            return result;
        }

        private void WriteTypes(Stream stream)
        {
            using(var writer = new PrimitiveWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(VersionNumber);
                writer.Write(typeArray.Length);
                for(var i = 0; i < typeArray.Length; i++)
                {
                    writer.Write(typeArray[i].AssemblyQualifiedName);
                }
            }
        }

        private void UpdateTypeIndices()
        {
            for(var i = 0; i < typeArray.Length; i++)
            {
                var element = typeArray[i];
                if(typeIndices.ContainsKey(element))
                {
                    continue;
                }
                typeIndices.Add(element, i);
            }
        }

        private readonly TypeScanner scanner;
        private Type[] typeArray;
        private readonly Dictionary<Type, int> typeIndices;

        private const ushort VersionNumber = 1;
        private const uint Magic = 0xA5132;
    }
}

