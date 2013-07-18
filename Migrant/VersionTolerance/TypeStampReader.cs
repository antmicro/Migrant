using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using AntMicro.Migrant.Customization;

namespace AntMicro.Migrant.VersionTolerance
{
	internal sealed class TypeStampReader
	{
		public TypeStampReader(PrimitiveReader reader, VersionToleranceLevel versionToleranceLevel)
		{
			this.reader = reader;
			this.versionToleranceLevel = versionToleranceLevel;
			stampCache = new Dictionary<Type, List<FieldInfoOrEntryToOmit>>();
		}

		public void ReadStamp(Type type)
		{
			if(!StampHelpers.IsStampNeeded(type))
			{
				return;
			}
			if(stampCache.ContainsKey(type))
			{
				return;
			}
			var result = new List<FieldInfoOrEntryToOmit>();
			var fieldNo = reader.ReadInt32();
			var moduleGuid = reader.ReadGuid();
			if(moduleGuid == type.Module.ModuleVersionId)
			{
				// short path, nothing to check
				for(var i = 0; i < fieldNo*2; i++)
				{
					reader.ReadString();
				}
				stampCache.Add(type, StampHelpers.GetFieldsInSerializationOrder(type, true).Select(x => new FieldInfoOrEntryToOmit(x)).ToList());
				return;
			}
			if(versionToleranceLevel == VersionToleranceLevel.GUID)
			{
				throw new InvalidOperationException(string.Format("The class was serialized with different module version id {0}, current one is {1}.",
				                                                  moduleGuid, type.Module.ModuleVersionId));
			}
			var currentFields = StampHelpers.GetFieldsInSerializationOrder(type, true).ToDictionary(x => x.Name, x => x);
			for(var i = 0; i < fieldNo; i++)
			{
				var fieldName = reader.ReadString();
				var fieldType = Type.GetType(reader.ReadString());
				FieldInfo currentField;
				if(!currentFields.TryGetValue(fieldName, out currentField))
				{
					// field is missing in the newer version of the class
					// we have to read the data to properly move stream,
					// but that's all - unless this is illegal from the
					// version tolerance level point of view
					if(versionToleranceLevel != VersionToleranceLevel.FieldRemoval && versionToleranceLevel != VersionToleranceLevel.FieldAdditionAndRemoval)
					{
						throw new InvalidOperationException(string.Format("Field {0} of type {1} was present in the old version of the class but is missing now. This is" +
						                                                  "incompatible with selected version tolerance level which is {2}.", fieldName, fieldType,
						                                                  versionToleranceLevel));
					}
					result.Add(new FieldInfoOrEntryToOmit(fieldType));
					continue;
				}
				// are the types compatible?
				if(currentField.FieldType != fieldType)
				{
					throw new InvalidOperationException(string.Format("Field {0} was of type {1} in the old version of the class, but currently is {2}. Cannot proceed.",
					                                                  fieldName, fieldType, currentField.FieldType));
				}
				// why do we remove a field from current ones? if some field is still left after our operation, then field addition occured
				// we have to check that, cause it can be illegal from the version tolerance point of view
				currentFields.Remove(fieldName);
				result.Add(new FieldInfoOrEntryToOmit(currentField));
			}
			// result should also contain transient fields, because some of them may
			// be marked with the [Constructor] attribute
			var transientFields = currentFields.Select(x => x.Value).Where(x => !Helpers.IsNotTransient(x)).Select(x => new FieldInfoOrEntryToOmit(x)).ToArray();
			if(currentFields.Count - transientFields.Length > 0 && versionToleranceLevel != VersionToleranceLevel.FieldAddition && 
			   versionToleranceLevel != VersionToleranceLevel.FieldAdditionAndRemoval)
			{
				throw new InvalidOperationException(string.Format("Current version of the class {0} contains more fields than it had when it was serialized. With given" +
				                                                  "version tolerance level {1} the serializer cannot proceed.", type, versionToleranceLevel));
			}
			result.AddRange(transientFields);
			stampCache.Add(type, result);
		}

		public IEnumerable<FieldInfoOrEntryToOmit> GetFieldsToDeserialize(Type type)
		{
			return stampCache[type];
		}

		private readonly Dictionary<Type, List<FieldInfoOrEntryToOmit>> stampCache;
		private readonly PrimitiveReader reader;
		private readonly VersionToleranceLevel versionToleranceLevel;
	}
}

