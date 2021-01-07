//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Hooks
{
    /// <summary>
    /// Method decorated with this attribute will be invoked after the whole
    /// serialization has been finished in the opposite order they were encountered
    /// during serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class LatePostSerializationAttribute : Attribute
    {

    }
}

