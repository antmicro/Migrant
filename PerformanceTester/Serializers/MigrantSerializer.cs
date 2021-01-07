//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.IO;

namespace Antmicro.Migrant.PerformanceTester.Serializers
{
	public sealed class MigrantSerializer : ISerializer
	{
		public MigrantSerializer()
		{
			serializer = new Serializer();
		}

		public void Serialize<T>(T what, Stream where)
		{
			serializer.Serialize(what, where);
		}

		public T Deserialize<T>(Stream from)
		{
			return serializer.Deserialize<T>(from);
		}

		private readonly Serializer serializer;
	}
}

