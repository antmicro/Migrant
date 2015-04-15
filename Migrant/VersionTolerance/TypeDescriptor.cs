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
using System.Reflection;
using System.Linq;
using Antmicro.Migrant.VersionTolerance;
using Antmicro.Migrant.Customization;
using System.Text.RegularExpressions;

namespace Antmicro.Migrant
{
    internal class TypeDescriptor
    {
        public static TypeDescriptor ReadFromStream(ObjectReader reader, VersionToleranceLevel versionToleranceLevel)
        {
            var result =  new TypeDescriptor();
            result.ReadTypeStamp(reader);
            result.versionToleranceLevel = versionToleranceLevel;

            return result;
        }

        public static TypeDescriptor CreateFromType(Type type)
        {
            if(!cache.ContainsKey(type))
            {
                cache[type] = new TypeDescriptor();
                // we need to call init after creating empty `TypeDescriptor`
                // and putting it in `Cache` as field types can refer to the
                // cache
                cache[type].Init(type);
            }

            return cache[type];
        }

        public void ReadStructureStampIfNeeded(ObjectReader reader)
        {
            if(StampHelpers.IsStampNeeded(this, reader.TreatCollectionAsUserObject))
            {
                ReadStructureStamp(reader);
            }
        }

        public void WriteStructureStampIfNeeded(ObjectWriter writer)
        {
            if(StampHelpers.IsStampNeeded(this, writer.TreatCollectionAsUserObject))
            {
                WriteStructureStamp(writer);
            }
        }

        public void WriteTypeStamp(ObjectWriter writer)
        {
            writer.TouchAndWriteAssemblyId(TypeAssembly);
            writer.PrimitiveWriter.Write(FullName);
            writer.PrimitiveWriter.Write(IsGenericType ? 1 : 0);
        }

        public Type Resolve()
        {
            if(type != null)
            {
                return type;
            }

            type = TypeAssembly.Resolve().GetType(fullName);
            if(type == null)
            {
                throw new InvalidOperationException(string.Format("Couldn't load type '{0}'", fullName));
            }
            fieldsToDeserialize = VerifyStructure();
           
            cache[type] = this;
            return type;
        }

        /// <summary>
        /// Resolves type and calculates which fields should be read and which ommited during deserialization.
        /// </summary>
        /// <returns>The collection of elements describing fields to deserialize or omit.</returns>
        public IEnumerable<FieldInfoOrEntryToOmit> GetFieldsToDeserialize()
        {
            Resolve();
            return fieldsToDeserialize;
        }

        public override int GetHashCode()
        {
            return AssemblyQualifiedName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var objAsTypeDescriptor = obj as TypeDescriptor;
            if(objAsTypeDescriptor != null)
            {
                return AssemblyQualifiedName == objAsTypeDescriptor.AssemblyQualifiedName;
            }

            return obj != null && obj.Equals(this);
        }

        public TypeDescriptorCompareResult CompareWith(TypeDescriptor previous)
        {
            var result = new TypeDescriptorCompareResult();

            var prevFields = previous.fields.ToDictionary(x => x.FullName, x => x);
            foreach(var field in fields.Where(f => !f.IsTransient))
            {
                FieldDescriptor currentField;
                if(!prevFields.TryGetValue(field.FullName, out currentField))
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
                prevFields.Remove(field.FullName);
            }

            // result should also contain transient fields, because some of them may
            // be marked with the [Constructor] attribute
            foreach(var nonTransient in prevFields.Values.Where(x => !x.IsTransient))
            {
                result.FieldsRemoved.Add(nonTransient);
            }

            return result;
        }

        public TypeDescriptor GetGenericTypeDefinition()
        {
            if(type != null)
            {
                return new TypeDescriptor((type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName, TypeAssembly, 0);
            }

            var index = fullName.IndexOf('[');
            return new TypeDescriptor(fullName.Substring(0, index), TypeAssembly, 1);
        }

        public bool IsGenericType { get { return type != null ? type.IsGenericType : isGenericType.Value; } }

        public string FullName { get { return type != null ? type.FullName : fullName; } }

        public string AssemblyQualifiedName
        { 
            get
            { 
                return type != null ? type.AssemblyQualifiedName : string.Format("{0}, {1}", FullName, TypeAssembly.FullName);
            }
        }

        public AssemblyDescriptor TypeAssembly { get; private set; }

        private TypeDescriptor()
        {
            fields = new List<FieldDescriptor>();
        }

        // TODO: this should be removed
        private TypeDescriptor(string fullName, AssemblyDescriptor assembly, int genericParameters) : this()
        {
            this.fullName = fullName;
            isGenericType = (genericParameters > 0);
            TypeAssembly = assembly;
        }

        private void Init(Type t)
        {
            type = t;

            TypeAssembly = AssemblyDescriptor.CreateFromAssembly(t.Assembly);
            fullName = type.FullName;
            isGenericType = type.IsGenericType;

            if(t.BaseType != null)
            {
                baseType = TypeDescriptor.CreateFromType(t.BaseType);
            }

            fieldsToDeserialize = new List<FieldInfoOrEntryToOmit>();
            foreach(var field in StampHelpers.GetFieldsInSerializationOrder(type, true))
            {
                fieldsToDeserialize.Add(new FieldInfoOrEntryToOmit(field));
                if(!field.IsTransient())
                {
                    fields.Add(new FieldDescriptor(field));
                }
            }
        }

        private IEnumerable<FieldInfoOrEntryToOmit> GetConstructorRecreatedFields()
        {
            Resolve();
            return fieldsToDeserialize.Where(x => x.Field != null && x.Field.IsConstructor());
        }

        private List<FieldInfoOrEntryToOmit> VerifyStructure()
        {
            if(TypeAssembly.ModuleGUID == type.Module.ModuleVersionId)
            {
                return StampHelpers.GetFieldsInSerializationOrder(type, true).Select(x => new FieldInfoOrEntryToOmit(x)).ToList();
            }

            if(!versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowGuidChange))
            {
                throw new InvalidOperationException(string.Format("The class was serialized with different module version id {0}, current one is {1}.",
                    TypeAssembly.ModuleGUID, type.Module.ModuleVersionId));
            }

            var result = new List<FieldInfoOrEntryToOmit>();

            var assemblyTypeDescriptor = TypeDescriptor.CreateFromType(type);
            if(!assemblyTypeDescriptor.baseType.Equals(baseType) && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowInheritanceChainChange))
            {
                throw new InvalidOperationException(string.Format("Class hierarchy changed. Expected {1} as base class, but found {0}.", baseType, assemblyTypeDescriptor.baseType));
            }

            if(assemblyTypeDescriptor.TypeAssembly.Version != TypeAssembly.Version && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowAssemblyVersionChange))
            {
                throw new InvalidOperationException(string.Format("Assembly version changed from {0} to {1} for class {2}", TypeAssembly.Version, assemblyTypeDescriptor.TypeAssembly.Version, FullName));
            }

            var cmpResult = assemblyTypeDescriptor.CompareWith(this);

            if(cmpResult.FieldsChanged.Any())
            {
                throw new InvalidOperationException(string.Format("Field {0} type changed.", cmpResult.FieldsChanged[0].Name));
            }

            if(cmpResult.FieldsAdded.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowFieldAddition))
            {
                throw new InvalidOperationException(string.Format("Field added: {0}.", cmpResult.FieldsAdded[0].Name));
            }
            if(cmpResult.FieldsRemoved.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowFieldRemoval))
            {
                throw new InvalidOperationException(string.Format("Field removed: {0}.", cmpResult.FieldsRemoved[0].Name));
            }

            foreach(var field in fields)
            {
                if(cmpResult.FieldsRemoved.Contains(field))
                {
                    result.Add(new FieldInfoOrEntryToOmit(field.FieldType.Resolve()));
                }
                else
                {
                    result.Add(new FieldInfoOrEntryToOmit(field.Resolve()));
                }
            }

            foreach(var field in assemblyTypeDescriptor.GetConstructorRecreatedFields().Select(x => x.Field))
            {
                result.Add(new FieldInfoOrEntryToOmit(field));
            }

            return result;
        }

        // this is just a temporary method that should be removed when assembly stamping is finished
        private static Version GetVersionFromFullName(string assemblyName)
        {
            var match = Regex.Match(assemblyName, @"^.* Version=([0-9.]*), Culture=[A-Za-z0-9]*, PublicKeyToken=[0-9A-Za-z-]*$");
            var versionString = match.Groups[1].Value;
            return new Version(versionString);
        }

        private void ReadTypeStamp(ObjectReader reader)
        {
            TypeAssembly = reader.ReadAssembly();
            fullName = reader.PrimitiveReader.ReadString();
            isGenericType = (reader.PrimitiveReader.ReadInt32() > 0);
        }

        private void ReadStructureStamp(ObjectReader reader)
        {
            baseType = reader.ReadType();
            var noOfFields = reader.PrimitiveReader.ReadInt32();
            for(int i = 0; i < noOfFields; i++)
            {
                var fieldDescriptor = new FieldDescriptor(this);
                fieldDescriptor.ReadFrom(reader);
                fields.Add(fieldDescriptor);
            }
        }

        private void WriteStructureStamp(ObjectWriter writer)
        {
            if(baseType == null)
            {
                writer.PrimitiveWriter.Write(Consts.NullObjectId);
            }
            else
            {
                writer.TouchAndWriteTypeId(baseType.Resolve());
            }

            writer.PrimitiveWriter.Write(fields.Count);
            foreach(var field in fields)
            {
                field.WriteTo(writer);
            }
        }

        private string fullName;
        private bool? isGenericType;

        private Type type;
        private TypeDescriptor baseType;
        private VersionToleranceLevel versionToleranceLevel;

        private readonly List<FieldDescriptor> fields;
        private List<FieldInfoOrEntryToOmit> fieldsToDeserialize;

        private static Dictionary<Type, TypeDescriptor> cache = new Dictionary<Type, TypeDescriptor>();

        public class TypeDescriptorCompareResult
        {
            public List<FieldDescriptor> FieldsRemoved { get; private set; }

            public List<FieldDescriptor> FieldsAdded { get; private set; }

            public List<FieldDescriptor> FieldsChanged { get; private set; }

            public bool Empty { get { return FieldsRemoved.Count == 0 && FieldsAdded.Count == 0 && FieldsChanged.Count == 0; } }

            public TypeDescriptorCompareResult()
            {
                FieldsRemoved = new List<FieldDescriptor>();
                FieldsAdded = new List<FieldDescriptor>();
                FieldsChanged = new List<FieldDescriptor>();
            }
        }
    }
}

