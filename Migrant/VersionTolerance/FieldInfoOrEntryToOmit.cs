//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection;

namespace Antmicro.Migrant.VersionTolerance
{
    internal sealed class FieldInfoOrEntryToOmit
    {
        public FieldInfoOrEntryToOmit(Type typeToOmit)
        {
            if(typeToOmit == null)
            {
                throw new ArgumentNullException();
            }
            this.TypeToOmit = typeToOmit;
        }

        public FieldInfoOrEntryToOmit(FieldInfo field)
        {
            if(field == null)
            {
                throw new ArgumentNullException();
            }
            this.Field = field;
        }

        public Type TypeToOmit { get; private set; }

        public FieldInfo Field { get; private set; }

        public override string ToString()
        {
            return string.Format("[FieldInfoOrEntryToOmit: TypeToOmit={0}, Field={1}]", TypeToOmit, Field);
        }
    }
}

