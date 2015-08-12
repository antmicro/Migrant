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
using System.Reflection;
using System.Collections.Generic;
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant
{
    internal class FieldDescriptor
    {
        public FieldDescriptor(TypeDescriptor declaringType)
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
            FieldType = reader.ReadType();
            DeclaringType = reader.ReadType();
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

        public TypeDescriptor DeclaringType { get; private set; }

        public TypeDescriptor FieldType { get; private set; }

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

