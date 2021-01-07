//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

namespace Antmicro.Migrant
{
    /// <summary>
    /// Enumeration describing possible desarialization operation results.
    /// </summary>
    public enum DeserializationResult
    {
        /// <summary>
        /// Deserialization succeeded.
        /// </summary>
        OK,
        /// <summary>
        /// Magic number was different than expected.
        /// </summary>
        WrongMagic,
        /// <summary>
        /// Serializer version mismatch. 
        /// </summary>
        WrongVersion,
        /// <summary>
        /// The type structure has changed in a not allowed way.
        /// </summary>
        TypeStructureChanged,
        /// <summary>
        /// Data in a stream was corrupted.
        /// </summary>
        StreamCorrupted,
        /// <summary>
        /// Type stamping configuration is inconsistent between stream and deserializer settings.
        /// </summary>
        WrongStreamConfiguration
    }
}

