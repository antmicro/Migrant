//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Hooks
{
    /// <summary>
    /// Method decorated with this attribute will be invoked after deserialization
    /// of given object and all objects referenced by this object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PostDeserializationAttribute : Attribute
    {

    }
}

