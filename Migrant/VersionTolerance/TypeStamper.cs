using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.VersionTolerance
{
	internal sealed class TypeStamper
	{
		public TypeStamper(PrimitiveWriter writer)
		{
			this.writer = writer;
			alreadyWritten = new HashSet<Type>();
		}

		public void Stamp(Type type)
		{
			if(!StampHelpers.IsStampNeeded(type))
			{
				return;
			}
			if(alreadyWritten.Contains(type))
			{
				return;
			}
			alreadyWritten.Add(type);
			var fields = StampHelpers.GetFieldsInSerializationOrder(type).ToArray();
			writer.Write(fields.Length);
			var moduleGuid = type.Module.ModuleVersionId;
			writer.Write(moduleGuid);
			foreach(var field in fields)
			{
				var fieldType = field.FieldType;
				writer.Write(field.Name);
				writer.Write(fieldType.AssemblyQualifiedName);
			}
		}

		private readonly PrimitiveWriter writer;
		private readonly HashSet<Type> alreadyWritten;
	}
}

