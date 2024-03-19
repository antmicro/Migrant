//
// Copyright (c) 2012-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace Antmicro.Migrant
{
    /// <summary>
    /// The exception thrown when an attempt is made to serialize a non-serializable type, such as a pointer, ThreadLocal or SpinLock.
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

        /// <summary>
        /// Initializes a new instance of the <see cref="NonSerializableTypeException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="obj">Non-serializable object which caused an error.</param>
        /// <param name="parents">Dictionary with objects as keys and collections of their parents as values.</param>
        public NonSerializableTypeException(string message, object obj, Dictionary<object, HashSet<object>> parents) : base(message)
        {
            var parentsObjects = new Dictionary<object, IEnumerable<object>>();
            foreach(var entry in parents)
            {
                parentsObjects[entry.Key] = entry.Value;
            }
            Data["parentsObjects"] = parentsObjects;
            Data["nonSerializableObject"] = obj;
        }
    }
}

