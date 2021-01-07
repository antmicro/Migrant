//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Tests
{
	public class CompanionSecondModule
	{
		public int Counter { get; set; }
		
		public void Method()
		{
			Counter++;
		}
	}
}

