
namespace AntMicro.AntSerializer
{
    // TODO: usage should be clarified
    // TODO: should not be in core
    public class ConstructorAttribute : TransientAttribute
    {
        public ConstructorAttribute(params object[] parameters)
        {
            this.parameters = parameters;
        }

        public object[] Parameters
        {
            get
            {
                return parameters;
            }
        }

        private object[] parameters;
    }
}

