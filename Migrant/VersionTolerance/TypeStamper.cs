using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.VersionTolerance
{
	internal sealed class TypeStamper
	{
		public TypeStamper(PrimitiveReader reader, PrimitiveWriter writer)
		{
			this.reader = reader;
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

		public IEnumerable<FieldInfo> VerifyAndProvideCompatibleFields(Type type)
		{
			// TODO: do not verify if field is primitive or special
			throw new NotImplementedException();
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

