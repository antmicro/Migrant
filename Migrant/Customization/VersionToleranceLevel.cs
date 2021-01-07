//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Customization
{
    /// <summary>
    /// Level of the version tolerance, that is how much layout of the deserialized type can differ
    /// from the (original) layout of the serialized type.
    /// </summary>
    [Flags]
    public enum VersionToleranceLevel
    {
        /// <summary>
        /// Difference in guid is allowed which means that classes may be from different compilation of the same library.
        /// </summary>
        AllowGuidChange = 0x1,

        /// <summary>
        /// The new layout can have more fields that the old one. They are initialized to their default values.
        /// This flag implies <see cref="AllowGuidChange"/>.
        /// </summary>
        AllowFieldAddition = 0x2 | AllowGuidChange,

        /// <summary>
        /// The new layout can have less fields that the old one. Values of the missing one are ignored.
        /// This flag implies <see cref="AllowGuidChange"/>.
        /// </summary>
        AllowFieldRemoval = 0x4 | AllowGuidChange,

        /// <summary>
        /// Classes inheritance hirarchy can vary between new and old layout, e.g., base class can be removed.
        /// This flag implies <see cref="AllowGuidChange"/>.
        /// </summary>
        AllowInheritanceChainChange = 0x80 | AllowGuidChange,

        /// <summary>
        /// Assemblies version can very between new and old layout.
        /// This flag implies <see cref="AllowGuidChange"/>.
        /// </summary>
        AllowAssemblyVersionChange = 0x10 | AllowGuidChange
    }
}

