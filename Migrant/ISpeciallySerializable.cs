//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

namespace Antmicro.Migrant
{
    /// <summary>
    /// When implemented, its method are used by serializer to save or load the state of
    /// the object. Implementing class must contain a parameterless constructor which is
    /// invoked during deserialization, immediately before calling the <see cref="Load" />
    /// method.
    /// </summary>
    public interface ISpeciallySerializable
    {
        /// <summary>
        /// Invoked by the serializer immediately after construction of object (using
        /// parameterless constructor). Should restore the state of the object, previously
        /// stored using the <see cref="Save" /> method.
        /// </summary>
        /// <param name='reader'>
        /// The reader which should be used to read the previously written data.
        /// </param>
        /// <remarks>
        /// This method must read the number of bytes that were earlier written. Otherwise
        /// the <see cref="System.InvalidOperationException" /> is thrown.
        /// </remarks>
        void Load(PrimitiveReader reader);

        /// <summary>
        /// Invoked by the serializer during serialization. Should store the state of the
        /// object which can be later read using the <see cref="Load" /> method.
        /// </summary>
        /// <param name='writer'>
        /// Writer used to save the state of the object.
        /// </param>
        void Save(PrimitiveWriter writer);
    }
}

