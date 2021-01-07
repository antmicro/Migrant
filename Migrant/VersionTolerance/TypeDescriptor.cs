//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;
using Antmicro.Migrant.VersionTolerance;
using System.Collections.Concurrent;
using Antmicro.Migrant.Utilities;
using System.Diagnostics;

namespace Antmicro.Migrant
{
    [DebuggerDisplay("{GenericFullName}")]
    internal abstract class TypeDescriptor : IIdentifiedElement
    {
        public string Name { get; protected set; }

        public Type UnderlyingType { get; protected set; }

        public IEnumerable<FieldInfoOrEntryToOmit> FieldsToDeserialize { get; protected set; }

        public abstract void Read(ObjectReader reader);

        public abstract void Write(ObjectWriter writer);

        public override int GetHashCode()
        {
            return UnderlyingType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var objAsTypeDescriptor = obj as TypeDescriptor;
            if(objAsTypeDescriptor != null)
            {
                return UnderlyingType == objAsTypeDescriptor.UnderlyingType;
            }

            return obj != null && obj.Equals(this);
        }

        protected static ConcurrentDictionary<Type, TypeSimpleDescriptor> simpleCache = new ConcurrentDictionary<Type, TypeSimpleDescriptor>();
        protected static ConcurrentDictionary<Type, TypeFullDescriptor> fullCache = new ConcurrentDictionary<Type, TypeFullDescriptor>();
    }
}

