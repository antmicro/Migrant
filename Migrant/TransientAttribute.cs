using System;

namespace AntMicro.Migrant
{
	/// <summary>
	/// When used on a class, it prevents the serialization of all fields which has the type of
	/// that class. When used on a field, it prevents the serialization of this field.
	/// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field)]
	public class TransientAttribute : Attribute
	{
		
	}
}

