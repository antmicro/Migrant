//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.IO;

namespace Antmicro.Migrant.PerformanceTester.Serializers
{
	public sealed class ProtobufSerializer : ISerializer
	{
		public void Serialize<T>(T what, Stream where)
		{
			ProtoBuf.Serializer.Serialize(where, what);
		}

		public T Deserialize<T>(Stream from)
		{
			return ProtoBuf.Serializer.Deserialize<T>(from);
		}
	}
}

