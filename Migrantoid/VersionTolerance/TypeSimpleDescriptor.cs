// *******************************************************************
//
//  Copyright (c) 2012-2014, Antmicro Ltd <antmicro.com>
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
using System.Text;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.VersionTolerance
{
    internal class TypeSimpleDescriptor : TypeDescriptor
    {
        public static implicit operator TypeSimpleDescriptor(Type type)
        {
            return simpleCache.GetOrAdd(type, x => new TypeSimpleDescriptor(x));
        }

        public TypeSimpleDescriptor()
        {
        }

        public TypeSimpleDescriptor(Type t)
        {
            UnderlyingType = t;
            Name = t.AssemblyQualifiedName;
            nameAsByteArray = Encoding.UTF8.GetBytes(Name);

            var fieldsToDeserialize = new List<FieldInfoOrEntryToOmit>();
            foreach(var field in StampHelpers.GetFieldsInSerializationOrder(UnderlyingType, true))
            {
                fieldsToDeserialize.Add(new FieldInfoOrEntryToOmit(field));
            }
            FieldsToDeserialize = fieldsToDeserialize;
        }

        public override void Read(ObjectReader reader)
        {
            var size = reader.PrimitiveReader.ReadInt32();
            nameAsByteArray = reader.PrimitiveReader.ReadBytes(size);
            Name = Encoding.UTF8.GetString(nameAsByteArray);

            UnderlyingType = TypeProvider.GetType(Name);
        }

        public override void Write(ObjectWriter writer)
        {
            writer.PrimitiveWriter.Write(nameAsByteArray.Length);
            writer.PrimitiveWriter.Write(nameAsByteArray);
        }

        // this field is used to store decoded string in order to improve performance of stamping
        private byte[] nameAsByteArray;
    }
}

