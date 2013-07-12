using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant.VersionTolerance
{
	internal static class StampHelpers
	{
		public static bool IsStampNeeded(Type type)
		{
			Type fake1;
			bool fake2, fake3, fake4;
			return !Helpers.IsWriteableByPrimitiveWriter(type) || !Helpers.IsCollection(type, out fake1, out fake2, out fake3, out fake4);
		}

		// TODO: find all the usages and use verify or sth there
		public static IEnumerable<FieldInfo> GetFieldsInSerializationOrder(Type type, bool withTransient = false)
		{
			return type.GetAllFields().Where(x => withTransient || Helpers.IsNotTransient(x)).OrderBy(x => x.Name);
		}
	}
}

