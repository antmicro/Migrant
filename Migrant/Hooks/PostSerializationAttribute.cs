using System;

namespace AntMicro.Migrant.Hooks
{
	/// <summary>
	/// Method decorated with this attribute will be invoked after serialization.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
    public class PostSerializationAttribute : Attribute
    {

    }
}

