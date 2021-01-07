//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.IO;

namespace Antmicro.Migrant.PerformanceTester.Serializers
{
	public interface ISerializer
	{
		void Serialize<T>(T what, Stream where);
		T Deserialize<T>(Stream from);
	}
}

