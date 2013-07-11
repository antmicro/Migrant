using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.VersionTolerance
{
	internal sealed class TypeStamper
	{
		public TypeStamper(PrimitiveReader reader)
		{
			this.reader = reader;
		}

		public TypeStamper(PrimitiveWriter writer)
		{
			this.writer = writer;
		}		

		public void Write(Type type)
		{
			// TODO: do not write stamps if field is primitive or special (collection etc)
			var fields = GetFieldsInSerializationOrder(type).ToArray();
			writer.Write(fields.Length);
			foreach(var field in fields)
			{
				writer.Write(field.Name);
				writer.Write(field.FieldType.AssemblyQualifiedName);
			}
		}

		public IEnumerable<FieldInfoOrEntryToOmit> VerifyAndProvideCompatibleFields(Type type)
		{
			// TODO: do not verify if field is primitive or special
			var result = new List<FieldInfoOrEntryToOmit>();
			var currentFields = GetFieldsInSerializationOrder(type).ToDictionary(x => x.Name, x => x);
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
			return result;
		}

		// TODO: find all the usages and use verify or sth there
		public static IEnumerable<FieldInfo> GetFieldsInSerializationOrder(Type type)
		{
			return type.GetAllFields().Where(Helpers.IsNotTransient).OrderBy(x => x.Name);
		}

		private readonly PrimitiveReader reader;
		private readonly PrimitiveWriter writer;

	}
}

