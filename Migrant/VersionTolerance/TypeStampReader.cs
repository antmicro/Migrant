using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.VersionTolerance
{
	internal sealed class TypeStampReader
	{
		public TypeStampReader(PrimitiveReader reader)
		{
			this.reader = reader;
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
			var currentFields = StampHelpers.GetFieldsInSerializationOrder(type, true).ToDictionary(x => x.Name, x => x);
			var fieldNo = reader.ReadInt32();
			for(var i = 0; i < fieldNo; i++)
			{
				var fieldName = reader.ReadString();
				var fieldType = Type.GetType(reader.ReadString());
				FieldInfo currentField;
				if(!currentFields.TryGetValue(fieldName, out currentField))
				{
					// field is missing in the newer version of the class
					// we have to read the data to properly move stream,
					// but that's all
					result.Add(new FieldInfoOrEntryToOmit(fieldType));
					continue;
				}
				// are the types compatible?
				if(currentField.FieldType != fieldType)
				{
					// TODO: better exception message
					throw new InvalidOperationException("Not compatible version.");
				}
				result.Add(new FieldInfoOrEntryToOmit(currentField));
			}
			// result should also contain transient fields, because some of them may
			// be marked with the [Constructor] attribute
			result.AddRange(currentFields.Select(x => x.Value).Where(x => !Helpers.IsNotTransient(x)).Select(x => new FieldInfoOrEntryToOmit(x)));
			stampCache.Add(type, result.OrderBy(x => x.Field == null ? x.TypeToOmit.Name : x.Field.FieldType.Name).ToList());
		}

		public IEnumerable<FieldInfoOrEntryToOmit> GetFieldsToDeserialize(Type type)
		{
			return stampCache[type];
		}

		private readonly Dictionary<Type, List<FieldInfoOrEntryToOmit>> stampCache;
		private readonly PrimitiveReader reader;
	}
}

