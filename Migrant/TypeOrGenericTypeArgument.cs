//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant
{
    internal struct TypeOrGenericTypeArgument
    {
        public TypeOrGenericTypeArgument(Type t) : this()
        {
            Type = t;
            GenericTypeArgumentIndex = -1;
        }

        public TypeOrGenericTypeArgument(int index) : this()
        {
            Type = null;
            GenericTypeArgumentIndex = index;
        }

        public Type Type { get; private set; }
        public int GenericTypeArgumentIndex { get; private set; }
    }
}

