using System;
using System.Reflection;

namespace AntMicro.Migrant.VersionTolerance
{
	public sealed class FieldInfoOrEntryToOmit
	{
		public FieldInfoOrEntryToOmit(Type typeToOmit)
		{
			this.TypeToOmit = typeToOmit;
		}

		public FieldInfoOrEntryToOmit(FieldInfo field)
		{
			this.Field = field;
		}		

		public Type TypeToOmit { get; private set; }
		public FieldInfo Field { get; private set; }

		public override string ToString()
		{
			return string.Format("[FieldInfoOrEntryToOmit: TypeToOmit={0}, Field={1}]", TypeToOmit, Field);
		}
	}
}

