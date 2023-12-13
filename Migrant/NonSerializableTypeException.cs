//
// Copyright (c) 2012-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Runtime.Serialization;

namespace Antmicro.Migrant
{
    /// <summary>
    /// The exception thrown when an attempt is made to serialize a non-serializable type, such as a pointer, ThreadLocal<> or SpinLock.
    /// </summary>
    [Serializable]
    public class NonSerializableTypeException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NonSerializableTypeException"/> class.
        /// </summary>
        public NonSerializableTypeException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NonSerializableTypeException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public NonSerializableTypeException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NonSerializableTypeException"/> class.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
        public NonSerializableTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NonSerializableTypeException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public NonSerializableTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

