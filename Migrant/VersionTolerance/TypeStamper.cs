using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.VersionTolerance
{
	// TODO: consider having two classes: Stamper and Destamper
	internal sealed class TypeStamper
	{
		public TypeStamper(PrimitiveReader reader)
		{
			this.reader = reader;
			stampCache = new Dictionary<Type, List<FieldInfoOrEntryToOmit>>();
		}

		public TypeStamper(PrimitiveWriter writer)
		{
			this.writer = writer;
			alreadyWritten = new HashSet<Type>();
		}

		public void Stamp(Type type)
		{
			if(!IsStampNeeded(type))
			{
				return;
			}
			if(alreadyWritten.Contains(type))
			{
				return;
			}
			alreadyWritten.Add(type);
			var fields = GetFieldsInSerializationOrder(type).ToArray();
			writer.Write(fields.Length);
			foreach(var field in fields)
			{
				var fieldType = field.FieldType;
				writer.Write(field.Name);
				writer.Write(fieldType.AssemblyQualifiedName);
			}
		}

		public void ReadStamp(Type type)
		{
			if(!IsStampNeeded(type))
			{
				return;
			}
			if(stampCache.ContainsKey(type))
			{
				return;
			}
			var result = new List<FieldInfoOrEntryToOmit>();
			var currentFields = GetFieldsInSerializationOrder(type, true).ToDictionary(x => x.Name, x => x);
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
			stampCache.Add(type, result);
		}

		// TODO: find all the usages and use verify or sth there
		public static IEnumerable<FieldInfo> GetFieldsInSerializationOrder(Type type, bool withTransient = false)
		{
			return type.GetAllFields().Where(x => withTransient || Helpers.IsNotTransient(x)).OrderBy(x => x.Name);
		}

		public IEnumerable<FieldInfo> GetFieldsToDeserialize(Type type)
		{
			// TODO: why select? simply hold only fields in cache
			return stampCache[type].Select(x => x.Field);
		}

		private static bool IsStampNeeded(Type type)
		{
			Type fake1;
			bool fake2, fake3, fake4;
			return !Helpers.IsWriteableByPrimitiveWriter(type) || !Helpers.IsCollection(type, out fake1, out fake2, out fake3, out fake4);
		}

		private readonly Dictionary<Type, List<FieldInfoOrEntryToOmit>> stampCache;
		private readonly PrimitiveReader reader;
		private readonly PrimitiveWriter writer;
		private readonly HashSet<Type> alreadyWritten;
	}
}

