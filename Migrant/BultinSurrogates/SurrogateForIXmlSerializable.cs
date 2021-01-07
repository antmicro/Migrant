//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.IO;
using System.Xml.Serialization;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal sealed class SurrogateForIXmlSerializable
    {
        public SurrogateForIXmlSerializable(IXmlSerializable serializable)
        {
            assemblyQualifiedName = serializable.GetType().AssemblyQualifiedName;

            using(var stream = new MemoryStream())
            {
                var xmlSerializer = new XmlSerializer(serializable.GetType());
                xmlSerializer.Serialize(stream, serializable);
                bytes = stream.ToArray();
            }
        }

        public IXmlSerializable Restore()
        {
            using(var stream = new MemoryStream(bytes))
            {
                var xmlSerializer = new XmlSerializer(TypeProvider.GetType(assemblyQualifiedName));
                return (IXmlSerializable)xmlSerializer.Deserialize(stream);
            }
        }

        private readonly string assemblyQualifiedName;
        private readonly byte[] bytes;
    }
}

