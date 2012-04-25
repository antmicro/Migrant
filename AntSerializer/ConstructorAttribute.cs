
namespace AntMicro.Migrant
{
    public class ConstructorAttribute : TransientAttribute
    {
        public ConstructorAttribute(params object[] parameters)
        {
            Parameters = parameters;
        }

        public object[] Parameters { get; private set; }

        
    }
}

