//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection;
using System.Collections.Generic;
using Antmicro.Migrant.Customization;
using Antmicro.Migrant.VersionTolerance;

namespace Antmicro.Migrant
{
    internal class FieldDescriptor
    {
        public FieldDescriptor(TypeFullDescriptor declaringType)
        {
            DeclaringType = declaringType;
        }

        public FieldDescriptor(FieldInfo finfo)
        {
            Name = finfo.Name;

            DeclaringType = finfo.DeclaringType;
            FieldType = finfo.FieldType;
            IsTransient = finfo.IsTransient();
            IsConstructor = finfo.IsConstructor();

            UnderlyingFieldInfo = finfo;
        }

        public void WriteTo(ObjectWriter writer)
        {
            writer.TouchAndWriteTypeId(FieldType.UnderlyingType);
            writer.TouchAndWriteTypeId(DeclaringType.UnderlyingType);
            writer.PrimitiveWriter.Write(Name);
        }

        public void ReadFrom(ObjectReader reader)
        {
            FieldType = (TypeFullDescriptor)reader.ReadType();
            DeclaringType = (TypeFullDescriptor)reader.ReadType();
            Name = reader.PrimitiveReader.ReadString();

            UnderlyingFieldInfo = DeclaringType.UnderlyingType.GetField(Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); 
        }

        public CompareResult CompareWith(FieldDescriptor fd, VersionToleranceLevel versionToleranceLevel)
        {
            if(fd.Name == Name)
            {
                if(!fd.FieldType.Equals(FieldType, versionToleranceLevel))
                {
                    return CompareResult.FieldTypeChanged;
                }
            }
            else if(fd.FieldType.Equals(FieldType))
            {
                if(fd.Name != Name)
                {
                    return CompareResult.FieldRenamed;
                }
            }
            else
            {
                return CompareResult.NotMatch;
            }

            return CompareResult.Match;
        }

        public override bool Equals(object obj)
        {
            var fd = obj as FieldDescriptor;
            if(fd != null)
            {
                return fd.Name == Name && fd.FieldType.Equals(FieldType) && fd.IsTransient == IsTransient && fd.DeclaringType.Equals(DeclaringType);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hash = 17;

            hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
            hash = hash * 23 + (FieldType != null ? FieldType.GetHashCode() : 0);
            hash = hash * 23 + IsTransient.GetHashCode();
            hash = hash * 23 + (DeclaringType != null ? DeclaringType.GetHashCode() : 0);

            return hash;
        }

        public bool IsConstructor { get; private set; }

        public bool IsTransient { get; private set; }

        public string Name { get; private set; }

        public string FullName { get { return string.Format("{0}:{1}", DeclaringType.UnderlyingType.FullName, Name); } }

        public TypeFullDescriptor DeclaringType { get; private set; }

        public TypeFullDescriptor FieldType { get; private set; }

        public FieldInfo UnderlyingFieldInfo { get; private set; }

        public enum CompareResult
        {
            Match,
            NotMatch,
            FieldRenamed,
            FieldTypeChanged
        }

        public class MoveFieldComparer : IEqualityComparer<FieldDescriptor>
        {
            public bool Equals(FieldDescriptor x, FieldDescriptor y)
            {
                return x.Name == y.Name && x.FieldType.Equals(y.FieldType);
            }

            public int GetHashCode(FieldDescriptor obj)
            {
                var hash = 17;

                hash = hash * 23 + (obj.Name != null ? obj.Name.GetHashCode() : 0);
                hash = hash * 23 + (obj.FieldType != null ? obj.FieldType.GetHashCode() : 0);

                return hash;
            }
        }
    }
}

