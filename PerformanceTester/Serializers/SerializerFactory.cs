//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.PerformanceTester.Serializers
{
	public static class SerializerFactory
	{
		public static ISerializer Produce(SerializerType type)
		{
			switch(type)
			{
			case SerializerType.Migrant:
				return new MigrantSerializer();
			case SerializerType.ProtoBuf:
				return new ProtobufSerializer();
			default:
				throw new ArgumentOutOfRangeException();
			}
		}
	}
}

