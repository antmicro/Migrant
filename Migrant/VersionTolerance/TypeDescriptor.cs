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
using System.Reflection;
using System.Linq;
using System.Text;

namespace Antmicro.Migrant
{
    internal class TypeDescriptor
    {
        public TypeDescriptor()
        {
            Fields = new List<FieldDescriptor>();
        }

        public TypeDescriptor(Tuple<Type, IEnumerable<FieldInfo>> type) : this()
        {
            AssemblyVersion = type.Item1.Assembly.GetName().Version;
            FullName = type.Item1.FullName;
            AssemblyName = type.Item1.Assembly.FullName;

            AssemblyQualifiedName = type.Item1.AssemblyQualifiedName;
            foreach(var field in type.Item2)
            {
                Fields.Add(new FieldDescriptor(field));
            }
        }

        public void WriteTo(PrimitiveWriter writer)
        {
            writer.Write(AssemblyQualifiedName);
            writer.Write(Fields.Count);
            foreach(var fl in Fields)
            {
                fl.WriteTo(writer);
            }
        }

        public void ReadFrom(PrimitiveReader reader)
        {
            AssemblyQualifiedName = reader.ReadString();
            var countNo = reader.ReadInt32();
            for(var i = 0; i < countNo; i++)
            {
                var field = new FieldDescriptor(AssemblyQualifiedName);
                field.ReadFrom(reader);
                Fields.Add(field);
            }
        }

        public TypeDescriptorCompareResult CompareWith(TypeDescriptor previous)
        {
            var result = new TypeDescriptorCompareResult();

            var prevFields = previous.Fields.ToDictionary(x => x.Name, x => x);
            foreach(var field in Fields.Where(f => !f.IsTransient))
            {
                FieldDescriptor currentField;
                if(!prevFields.TryGetValue(field.Name, out currentField))
                {
                    // field is missing in the previous version of the class
                    result.FieldsAdded.Add(field);
                    continue;
                }
                // are the types compatible?
                var compareResult = currentField.CompareWith(field);
                if(compareResult != FieldDescriptor.CompareResult.Match)
                {
                    result.FieldsChanged.Add(field);
                }

                // why do we remove a field from current ones? if some field is still left after our operation, then field addition occured
                // we have to check that, cause it can be illegal from the version tolerance point of view
                prevFields.Remove(field.Name);
            }

            // result should also contain transient fields, because some of them may
            // be marked with the [Constructor] attribute
            foreach(var nonTransient in prevFields.Values.Where(x => !x.IsTransient))
            {
                result.FieldsRemoved.Add(nonTransient);
            }

            return result;
        }

        public bool HasSameLayout(TypeDescriptor td)
        {
            if(Fields.Count != td.Fields.Count)
            {
                return false;
            }

            for(var i = 0; i < Fields.Count; i++)
            {
                if(Fields[i].CompareWith(td.Fields[i]) != FieldDescriptor.CompareResult.Match)
                {
                    return false;
                }
            }

            return true;
        }

        public string AssemblyName { get; private set; }

        public Version AssemblyVersion { get; private set; }

        public string FullName { get; private set; }

        public string AssemblyQualifiedName { get; private set; }

        public List<FieldDescriptor> Fields { get; private set; }

        public class TypeDescriptorCompareResult
        {
            public List<FieldDescriptor> FieldsRemoved { get; private set; }

            public List<FieldDescriptor> FieldsAdded { get; private set; }

            public List<FieldDescriptor> FieldsChanged { get; private set; }

            public TypeDescriptorCompareResult()
            {
                FieldsRemoved = new List<FieldDescriptor>();
                FieldsAdded = new List<FieldDescriptor>();
                FieldsChanged = new List<FieldDescriptor>();
            }
        }
    }
}

