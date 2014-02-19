/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

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
using System.Collections.Generic;
using System.IO;
using Antmicro.Migrant.Customization;
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;
using Migrant.BultinSurrogates;

namespace Antmicro.Migrant
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
		/// Initializes a new instance of the <see cref="Antmicro.Migrant.Serializer"/> class.
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
			writeMethodCache = new Dictionary<Type, DynamicMethod>();
            objectsForSurrogates = new InheritanceAwareList<Delegate>();
            surrogatesForObjects = new InheritanceAwareList<Delegate>();
			readMethodCache = new Dictionary<Type, DynamicMethod>();

            if(settings.SupportForISerializable)
            {
                ForObject<System.Runtime.Serialization.ISerializable>().SetSurrogate(x => new SurrogateForISerializable(x));
                ForSurrogate<SurrogateForISerializable>().SetObject(x => x.Restore());
                ForObject<Delegate>().SetSurrogate(x => x); //because Delegate implements ISerializable but we support it directly.
            }
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
		public void Serialize(object obj, Stream stream)
		{
			WriteHeader(stream);
            var writer = new ObjectWriter(stream, OnPreSerialization, OnPostSerialization, 
                writeMethodCache, surrogatesForObjects, settings.SerializationMethod == Method.Generated, settings.TreatCollectionAsUserObject);
			writer.WriteObject(obj);
			serializationDone = true;
		}

		/// <summary>
		/// Gives the ability to set callback providing object for surrogate of given type. The object will be provided instead of such
		/// surrogate in the effect of deserialization.
		/// </summary>
		/// <returns>
		/// Object letting you set the object for the given surrogate type.
		/// </returns>
		/// <typeparam name='TSurrogate'>
		/// The type for which callback will be invoked.
		/// </typeparam>
		public ObjectForSurrogateSetter<TSurrogate> ForSurrogate<TSurrogate>()
		{
			return new ObjectForSurrogateSetter<TSurrogate>(this);
		}

		/// <summary>
		/// Gives the ability to set callback providing surrogate for objects of given type. The surrogate will be serialized instead of 
		/// the object of that type.
		/// </summary>
		/// <returns>
		/// Object letting you set the surrogate for the given type.
		/// </returns>
		/// <typeparam name='TObject'>
		/// The type for which callback will be invoked.
		/// </typeparam>
		public SurrogateForObjectSetter<TObject> ForObject<TObject>()
		{
			return new SurrogateForObjectSetter<TObject>(this);
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
			// Read header
			var magic1 = stream.ReadByte();
			var magic2 = stream.ReadByte();
			var magic3 = stream.ReadByte();
			if(magic1 != Magic1 || magic2 != Magic2 || magic3 != Magic3)
			{
				throw new InvalidOperationException(string.Format(
					"Cound not find proper magic {0}, {1}, {2}, instead {3}, {4}, {5} was read.", Magic1, Magic2, Magic3,
					magic1, magic2, magic3));
			}
			var version = stream.ReadByte();
			if(version != VersionNumber)
			{
				throw new InvalidOperationException(string.Format(
					"Could not deserialize data serialized with another version of serializer, namely {0}. Current is {1}.", version, VersionNumber));
			}

			var objectReader = new ObjectReader(stream, objectsForSurrogates, OnPostDeserialization, readMethodCache,
                settings.DeserializationMethod == Method.Generated, settings.TreatCollectionAsUserObject, settings.VersionTolerance);
			var result = objectReader.ReadObject<T>();
			deserializationDone = true;
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

		private void WriteHeader(Stream stream)
		{
			stream.WriteByte(Magic1);
			stream.WriteByte(Magic2);
			stream.WriteByte(Magic3);
			stream.WriteByte(VersionNumber);
		}

		private bool serializationDone;
		private bool deserializationDone;
		private readonly Settings settings;
		private readonly Dictionary<Type, DynamicMethod> writeMethodCache;
		private readonly Dictionary<Type, DynamicMethod> readMethodCache;
        private readonly InheritanceAwareList<Delegate> surrogatesForObjects;
        private readonly InheritanceAwareList<Delegate> objectsForSurrogates;
		private const byte VersionNumber = 1;
		private const byte Magic1 = 0x32;
		private const byte Magic2 = 0x66;
		private const byte Magic3 = 0x34;

		/// <summary>
		/// Lets you set a callback providing object for type of the surrogate given to method that provided
		/// this object on a serializer that provided this object.
		/// </summary>
		public class ObjectForSurrogateSetter<TSurrogate>
		{
			internal ObjectForSurrogateSetter(Serializer serializer)
			{
				this.serializer = serializer;
			}

			/// <summary>
			/// Sets the callback proividing object for surrogate.
			/// </summary>
			/// <param name='callback'>
			/// Callback proividing object for surrogate.
			/// </param>
			/// <typeparam name='TObject'>
			/// The type of the object returned by callback.
			/// </typeparam>
			public void SetObject<TObject>(Func<TSurrogate, TObject> callback)
			{
				if(serializer.deserializationDone)
				{
					throw new InvalidOperationException("Cannot set objects for surrogates after any deserialization is done.");
				}
                serializer.objectsForSurrogates.AddOrReplace(typeof(TSurrogate), callback);
			}

			private readonly Serializer serializer;
		}

		/// <summary>
		/// Lets you set a callback providing surrogate for type of the object given to method that provided
		/// this object on a serializer that provided this object.
		/// </summary>
		public class SurrogateForObjectSetter<TObject>
		{
			internal SurrogateForObjectSetter(Serializer serializer)
			{
				this.serializer = serializer;
			}

			/// <summary>
			/// Sets the callback providing surrogate for object.
			/// </summary>
			/// <param name='callback'>
			/// Callback providing surrogate for object.
			/// </param>
			/// <typeparam name='TSurrogate'>
			/// The type of the object returned by callback.
			/// </typeparam>
			public void SetSurrogate<TSurrogate>(Func<TObject, TSurrogate> callback)
			{
				if(serializer.serializationDone)
				{
					throw new InvalidOperationException("Cannot set surrogates for objects after any serialization is done.");
				}
                serializer.surrogatesForObjects.AddOrReplace(typeof(TObject), callback);
			}

			private readonly Serializer serializer;
		}
	}
}

