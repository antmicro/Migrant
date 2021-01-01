// *******************************************************************
//
//  Copyright (c) 2012-2015, Antmicro Ltd <antmicro.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// *******************************************************************
using System;
using System.Collections.Generic;
using Migrantoid.VersionTolerance;
using System.Collections.Concurrent;
using Migrantoid.Utilities;
using System.Diagnostics;

namespace Migrantoid
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

