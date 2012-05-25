using System;

namespace AntMicro.Migrant
{
	[AttributeUsage(AttributeTargets.Field)]
	/// <summary>
	/// When this attribute is placed on a field, such field is filled with
	/// the newly constructed object of a same type as field. To construct
	/// such object, the parameters given in a attribute are used.
	/// </summary>
    public class ConstructorAttribute : TransientAttribute
    {
        public ConstructorAttribute(params object[] parameters)
        {
            Parameters = parameters;
        }

        public object[] Parameters { get; private set; }

        
    }
}

