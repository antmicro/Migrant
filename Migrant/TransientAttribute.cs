//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant
{
    /// <summary>
    /// When used on a class, it prevents the serialization of all fields which have the type of
    /// that class. When used on a field, it prevents the serialization of this field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Event)]
    public class TransientAttribute : Attribute
    {
    }
}

