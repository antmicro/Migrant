//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant
{
    /// <summary>
    /// When this attribute is placed on a field, such field is filled with
    /// the newly constructed object of a same type as field. To construct
    /// such object, the parameters given in a attribute are used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ConstructorAttribute : TransientAttribute
    {
        /// <summary>
        /// Initializes attribute with given parameters which will be later passed to the
        /// constructor.
        /// </summary>
        /// <param name='parameters'>
        /// Parameters.
        /// </param>
        public ConstructorAttribute(params object[] parameters)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// Parameters passed to the attribute.
        /// </summary>
        public object[] Parameters { get; private set; }

        
    }
}

