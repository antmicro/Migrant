using System;

namespace AntMicro.Migrant
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field)]
	public class TransientAttribute : Attribute
	{
		
	}
}

