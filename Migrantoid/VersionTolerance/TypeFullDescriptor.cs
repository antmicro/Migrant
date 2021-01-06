// *******************************************************************
//
//  Copyright (c) 2012-2014, Antmicro Ltd <antmicro.com>
//  Copyright (c) 2021, Konrad Kruczyński
//
//  Authors:
//   * Mateusz Holenko(mholenko@antmicro.com)
//   * Konrad Kruczyński (konrad.kruczynski@gmail.com)
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
using Migrantoid.Customization;
using System.Linq;

namespace Migrantoid.VersionTolerance
{
    internal class TypeFullDescriptor : TypeDescriptor
    {
        public static implicit operator TypeFullDescriptor(Type type)
        {
            var justCreated = false;
            var result = fullCache.GetOrAdd(type, x =>
            {
                justCreated = true;
                return new TypeFullDescriptor();
            });

            if(justCreated)
            {
                // we need to call init after creating empty `TypeDescriptor`
                // and putting it in cache as field types can refer to the
                // cache
                result.Init(type);
            }

            return result;
        }

        public TypeFullDescriptor()
        {
            fields = new List<FieldDescriptor>();
        }

        public override void Read(ObjectReader reader)
        {
            ReadStamp(reader);
            ReadStructureStampIfNeeded(reader, reader.VersionToleranceLevel, reader.ForceStampVerification);
        }

        public void ReadStamp(ObjectReader reader)
        {
            TypeModule = reader.Modules.Read();
            GenericFullName = reader.PrimitiveReader.ReadString();

            Resolve();
        }

        public override void Write(ObjectWriter writer)
        {
            WriteTypeStamp(writer);
            WriteStructureStampIfNeeded(writer);
        }

        public void ReadStructureStampIfNeeded(ObjectReader reader, VersionToleranceLevel versionToleranceLevel, bool forceStampVerification = false)
        {
            if(reader.recipes.ContainsKey(UnderlyingType))
            {
                // We do not need full stamp for recipe types.
                return;
            }

            if(StampHelpers.IsStampNeeded(this, reader.TreatCollectionAsUserObject))
            {
                ReadStructureStamp(reader, versionToleranceLevel, forceStampVerification);
            }
        }

        public void WriteStructureStampIfNeeded(ObjectWriter writer)
        {
            if (writer.recipes.ContainsKey(UnderlyingType))
            {
                // We do not need full stamp for recipe types.
                return;
            }

            if (StampHelpers.IsStampNeeded(this, writer.TreatCollectionAsUserObject))
            {
                WriteStructureStamp(writer);
            }
        }

        public void WriteTypeStamp(ObjectWriter writer)
        {
            writer.Modules.TouchAndWriteId(TypeModule);
            writer.PrimitiveWriter.Write(GenericFullName);
        }

        public bool Equals(TypeFullDescriptor obj, VersionToleranceLevel versionToleranceLevel)
        {
            if(versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowAssemblyVersionChange))
            {
                return obj.UnderlyingType.FullName == UnderlyingType.FullName
                    && obj.TypeModule.Equals(TypeModule, versionToleranceLevel);
            }

            return Equals(obj);
        }

        public TypeDescriptorCompareResult CompareWith(TypeFullDescriptor previous, VersionToleranceLevel versionToleranceLevel = 0)
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
                var compareResult = currentField.CompareWith(field, versionToleranceLevel);
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

        public string GenericFullName { get; private set; }
      
        public ModuleDescriptor TypeModule { get; private set; }

        private void Init(Type t)
        {
            UnderlyingType = t;

            TypeModule = new ModuleDescriptor(t.Module);
            if(UnderlyingType.IsGenericType)
            {
                GenericFullName = UnderlyingType.GetGenericTypeDefinition().FullName;
                Name = UnderlyingType.GetGenericTypeDefinition().AssemblyQualifiedName;
            }
            else
            {
                Name = UnderlyingType.AssemblyQualifiedName;
                GenericFullName = UnderlyingType.FullName;
            }

            if(t.BaseType != null)
            {
                baseType = t.BaseType;
            }

            var fieldsToDeserialize = new List<FieldInfoOrEntryToOmit>();
            foreach(var field in StampHelpers.GetFieldsInSerializationOrder(UnderlyingType, true))
            {
                fieldsToDeserialize.Add(new FieldInfoOrEntryToOmit(field));
                if(!field.IsTransient())
                {
                    fields.Add(new FieldDescriptor(field));
                }
            }
            FieldsToDeserialize = fieldsToDeserialize;
        }

        private void Resolve()
        {
            var type = TypeModule.ModuleAssembly.UnderlyingAssembly.GetType(GenericFullName);
            if(type == null)
            {
                throw new InvalidOperationException(string.Format("Couldn't load type '{0}'", GenericFullName));
            }

            Name = type.AssemblyQualifiedName;
            UnderlyingType = type;
        }

        private IEnumerable<FieldInfoOrEntryToOmit> GetConstructorRecreatedFields()
        {
            return FieldsToDeserialize.Where(x => x.Field != null && x.Field.IsConstructor());
        }

        private List<FieldInfoOrEntryToOmit> VerifyStructure(VersionToleranceLevel versionToleranceLevel, bool forceStampVerification)
        {
            if(TypeModule.GUID != UnderlyingType.Module.ModuleVersionId)
            {
                if(!versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowGuidChange))
                {
                    throw new VersionToleranceException(string.Format("The class {2} was serialized with different module version id {0}, current one is {1}.",
                        TypeModule.GUID, UnderlyingType.Module.ModuleVersionId, UnderlyingType.FullName));
                }
            }
            else if(!forceStampVerification)
            {
                return StampHelpers.GetFieldsInSerializationOrder(UnderlyingType, true).Select(x => new FieldInfoOrEntryToOmit(x)).ToList();
            }

            var result = new List<FieldInfoOrEntryToOmit>();

            var assemblyTypeDescriptor = ((TypeFullDescriptor)UnderlyingType);
            if( !(assemblyTypeDescriptor.baseType == null && baseType == null)
                && ((assemblyTypeDescriptor.baseType == null && baseType != null) || !assemblyTypeDescriptor.baseType.Equals(baseType)) 
                && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowInheritanceChainChange))
            {
                throw new VersionToleranceException(string.Format("Class hierarchy for {2} changed. Expected '{1}' as base class, but found '{0}'.", 
                    baseType != null ? baseType.UnderlyingType.FullName : "null", 
                    assemblyTypeDescriptor.baseType != null ? assemblyTypeDescriptor.baseType.UnderlyingType.FullName : "null",
                    UnderlyingType.FullName));
            }

            if(assemblyTypeDescriptor.TypeModule.ModuleAssembly.Version != TypeModule.ModuleAssembly.Version && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowAssemblyVersionChange))
            {
                throw new VersionToleranceException(string.Format("Assembly version changed from {0} to {1} for class {2}", 
                    TypeModule.ModuleAssembly.Version, assemblyTypeDescriptor.TypeModule.ModuleAssembly.Version, UnderlyingType.FullName));
            }

            var cmpResult = assemblyTypeDescriptor.CompareWith(this, versionToleranceLevel);

            if(cmpResult.FieldsChanged.Any())
            {
                throw new VersionToleranceException(string.Format("Field {0} type changed in class {1}.", cmpResult.FieldsChanged[0].Name, UnderlyingType.FullName));
            }

            if(cmpResult.FieldsAdded.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowFieldAddition))
            {
                throw new VersionToleranceException(string.Format("Field {0} added to class {1}.", cmpResult.FieldsAdded[0].Name, UnderlyingType.FullName));
            }
            if(cmpResult.FieldsRemoved.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowFieldRemoval))
            {
                throw new VersionToleranceException(string.Format("Field {0} removed from class {1}.", cmpResult.FieldsRemoved[0].Name, UnderlyingType.FullName));
            }

            foreach(var field in fields)
            {
                if(cmpResult.FieldsRemoved.Contains(field))
                {
                    result.Add(new FieldInfoOrEntryToOmit(field.FieldType.UnderlyingType));
                }
                else
                {
                    result.Add(new FieldInfoOrEntryToOmit(field.UnderlyingFieldInfo));
                }
            }

            foreach(var field in assemblyTypeDescriptor.GetConstructorRecreatedFields().Select(x => x.Field))
            {
                result.Add(new FieldInfoOrEntryToOmit(field));
            }

            return result;
        }

        private void ReadStructureStamp(ObjectReader reader, VersionToleranceLevel versionToleranceLevel, bool forceStampVerification)
        {
            baseType = (TypeFullDescriptor)reader.ReadType();
            var noOfFields = reader.PrimitiveReader.ReadInt32();
            for(int i = 0; i < noOfFields; i++)
            {
                var fieldDescriptor = new FieldDescriptor(this);
                fieldDescriptor.ReadFrom(reader);
                fields.Add(fieldDescriptor);
            }
            FieldsToDeserialize = VerifyStructure(versionToleranceLevel, forceStampVerification);
            // TODO: do we need this line?
            fullCache[UnderlyingType] = this;
        }

        private void WriteStructureStamp(ObjectWriter writer)
        {
            if(baseType == null)
            {
                writer.PrimitiveWriter.Write(Consts.NullObjectId);
            }
            else
            {
                writer.TouchAndWriteTypeId(baseType.UnderlyingType);
            }

            writer.PrimitiveWriter.Write(fields.Count);
            foreach(var field in fields)
            {
                field.WriteTo(writer);
            }
        }

        private TypeFullDescriptor baseType;

        private readonly List<FieldDescriptor> fields;

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

