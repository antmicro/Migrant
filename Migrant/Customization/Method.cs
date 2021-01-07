//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

namespace Antmicro.Migrant.Customization
{
    /// <summary>
    /// Method of serialization or deserialization.
    /// </summary>
    public enum Method
    {
        /// <summary>
        /// (De)Serialization is done using write method generator. For any type which encountered for the first time, the methods to serialize it are
        /// generated.
        /// </summary>
        Generated,

        /// <summary>
        /// (De)Serialization is done directly using reflection.
        /// </summary>
        Reflection
    }
}

