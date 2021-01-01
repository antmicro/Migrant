// *******************************************************************
//
//  Copyright (c) 2014, Antmicro Ltd
//  Author:
//    Konrad Kruczyński (kkruczynski@antmicro.com)
//    jpierson (https://github.com/jpierson)
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

