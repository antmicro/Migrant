//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

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

