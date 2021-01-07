//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Customization
{
    /// <summary>
    /// Used with open stream serialization and tells serializer how to deal with references
    /// between different sessions of serialization.
    /// </summary>
    public enum ReferencePreservation
    {
        /// <summary>
        /// If the same object is written in a different serialization session, it is written as 
        /// a newly encountered object.
        /// </summary>
        DoNotPreserve,

        /// <summary>
        /// Identity of all objects is preserved between sessions. This option will, however, create
        /// a hard reference for each so far serialized object. This means that all objects serialized
        /// so for will live until open stream serializer is disposed.
        /// </summary>
        Preserve,

        /// <summary>
        /// Object identity is preserved without hard references. This option is conceptually the best one,
        /// can however vastly influence performance.
        /// </summary>
        UseWeakReference
    }
}

