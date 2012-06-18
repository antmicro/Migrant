/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

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
using System.Collections.Generic;
using System.IO;
using AntMicro.Migrant.Emitter;
using AntMicro.Migrant.Customization;

namespace AntMicro.Migrant
{
	/// <summary>
	/// Provides the mechanism for binary serialization and deserialization of objects.
	/// </summary>
	/// <remarks>
	/// Please consult the general serializer documentation to find the limitations
	/// and constraints which serialized objects must fullfill.
	/// </remarks>
    public class Serializer
    {
		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.Serializer"/> class.
		/// </summary>
		/// <param name='settings'>
		/// Serializer's settings, can be null or not given, in that case default settings are
		/// used.
		/// </param>
        public Serializer(Settings settings = null)
        {
			if(settings == null)
			{
				settings = new Settings(); // default settings
			}
			this.settings = settings;
            scanner = new TypeScanner();
            typeArray = new Type[0];
            typeIndices = new Dictionary<Type, int>();
        }

		/// <summary>
		/// Initializes the given type (and its base and field types recursively). It does the
		/// initial check whether it is serializable and prepares the serializer.
		/// </summary>
		/// <param name='typeToScan'>
		/// Type to scan.
		/// </param>
        public void Initialize(Type typeToScan)
        {
            scanner.Scan(typeToScan);
            typeArray = scanner.GetTypeArray();
            UpdateTypeIndices();
        }

		/// <summary>
		/// Serializes the specified object to a given stream.
		/// </summary>
		/// <param name='obj'>
		/// Object to serialize along with its references.
		/// </param>
		/// <param name='stream'>
		/// Stream to which the given object should be serialized. Has to be writeable.
		/// </param>
		/// </param>
        public void Serialize(object obj, Stream stream)
        {
			// TODO: change memoryStream to lazy type information
            var localStream = new MemoryStream();
			ObjectWriter writer;
			if(settings.SerializationMethod == Method.Generated)
			{
				writer = new GeneratingObjectWriter(localStream, typeIndices, Initialize, OnPreSerialization, OnPostSerialization);
			}
			else
			{
				writer = new ObjectWriter(localStream, typeIndices, Initialize, OnPreSerialization, OnPostSerialization);
			}
            writer.WriteObject(obj);
            WriteTypes(stream);
            localStream.Seek(0, SeekOrigin.Begin);
            localStream.CopyTo(stream);
        }

		/// <summary>
		/// Deserializes object from the specified stream.
		/// </summary>
		/// <param name='stream'>
		/// The stream to read data from. Must be readable.
		/// </param>
		/// <typeparam name='T'>
		/// The expected type of the deserialized object. The deserialized object must be
		/// convertible to this type.
		/// </typeparam>
        public T Deserialize<T>(Stream stream)
        {
			if(settings.DeserializationMethod == Method.Generated)
			{
				throw new NotImplementedException("Generated deserialization is not yet implemented.");
			}
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

		/// <summary>
		/// Is invoked before serialization, once for every unique, serialized object. Provides this
		/// object in its single parameter.
		/// </summary>
        public event Action<object> OnPreSerialization;

		/// <summary>
		/// Is invoked after serialization, once for every unique, serialized object. Provides this
		/// object in its single parameter.
		/// </summary>
        public event Action<object> OnPostSerialization;

		/// <summary>
		/// Is invoked before deserialization, once for every unique, serialized object. Provides this
		/// object in its single parameter.
		/// </summary>
        public event Action<object> OnPostDeserialization;

		/// <summary>
		/// Makes a deep copy of a given object using the serializer.
		/// </summary>
		/// <returns>
		/// The deep copy of a given object.
		/// </returns>
		/// <param name='toClone'>
		/// The object to make a deep copy of.
		/// </param>
		/// <param name='settings'>
		/// Settings used for serializer which does deep clone.
		/// </param>
        public static T DeepClone<T>(T toClone, Settings settings = null)
        {
            var serializer = new Serializer(settings);
            var stream = new MemoryStream();
            serializer.Serialize(toClone, stream);
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
		private readonly Settings settings;

        private const ushort VersionNumber = 1;
        private const uint Magic = 0xA5132;
    }
}

