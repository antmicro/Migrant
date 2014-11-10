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
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Antmicro.Migrant
{
    internal class FieldDescriptor
    {
        public FieldDescriptor(string owningTypeAQN)
        { 
            OwningTypeAQN = owningTypeAQN;
        }

        public FieldDescriptor(FieldInfo finfo)
        {
            OwningTypeAQN = finfo.DeclaringType.AssemblyQualifiedName;
            Name = finfo.Name;
            TypeAQN = finfo.FieldType.AssemblyQualifiedName;
            IsTransient = finfo.GetCustomAttributes(false).Any(a => a is TransientAttribute);
            IsConstructor = finfo.GetCustomAttributes(false).Any(a => a is ConstructorAttribute);
        }

        public void WriteTo(PrimitiveWriter writer)
        {
            writer.Write(Name);
            writer.Write(TypeAQN);
        }

        public void ReadFrom(PrimitiveReader reader)
        {
            Name = reader.ReadString();
            TypeAQN = reader.ReadString();
        }

        public CompareResult CompareWith(FieldDescriptor fd)
        {
            if(fd.Name == Name)
            {
                if(fd.TypeAQN != TypeAQN)
                {
                    return CompareResult.FieldTypeChanged;
                }
            }
            else if(fd.TypeAQN == TypeAQN)
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
                return fd.Name == Name && fd.TypeAQN == TypeAQN && fd.IsTransient == IsTransient && fd.OwningTypeAQN == OwningTypeAQN;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hash = 17;

            hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
            hash = hash * 23 + (TypeAQN != null ? TypeAQN.GetHashCode() : 0);
            hash = hash * 23 + IsTransient.GetHashCode();
            hash = hash * 23 + (OwningTypeAQN != null ? OwningTypeAQN.GetHashCode() : 0);

            return hash;
        }

        public bool IsConstructor { get; private set; }

        public bool IsTransient { get; private set; }

        public string Name { get; private set; }

        public string TypeAQN { get; private set; }

        public string OwningTypeAQN { get; set; }

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
                return x.Name == y.Name && x.TypeAQN == y.TypeAQN;
            }

            public int GetHashCode(FieldDescriptor obj)
            {
                var hash = 17;

                hash = hash * 23 + (obj.Name != null ? obj.Name.GetHashCode() : 0);
                hash = hash * 23 + (obj.TypeAQN != null ? obj.TypeAQN.GetHashCode() : 0);

                return hash;
            }
        }
    }
}

