//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Linq;
using System.Reflection;

namespace Antmicro.Migrant.Generators
{
    internal abstract class DynamicMethodGenerator<T> where T : class
    {
        protected DynamicMethodGenerator(Type type, bool disableStamping = false, bool treatCollectionAsUserObject = false)
        {
            this.type = type;
            this.disableStamping = disableStamping;
            this.treatCollectionAsUserObject = treatCollectionAsUserObject;

            var invokeMethod = typeof(T).GetMethod("Invoke");
            if(invokeMethod == null)
            {
                throw new ArgumentException("DynamicMethodGenerator's generic type must be a delegate");
            }
            
            returnType = invokeMethod.ReturnType;
            parameterTypes = invokeMethod.GetParameters().Select(x => x.ParameterType).ToArray();
        }

        public T Generate()
        {
            var methodInfo = GenerateInner();
            if(methodInfo == null)
            {
                return null;
            }
            return (T)(object)methodInfo.CreateDelegate(typeof(T));
        }

        public bool TryGenerate(out T result)
        {
            var methodInfo = GenerateInner();
            if(methodInfo == null)
            {
                result = default(T);
                return false;
            }
            result = (T)(object)methodInfo.CreateDelegate(typeof(T));
            return true;
        }

        protected abstract MethodInfo GenerateInner();

        protected readonly Type returnType;
        protected readonly Type[] parameterTypes;

        protected readonly bool disableStamping;
        protected readonly bool treatCollectionAsUserObject;

        protected readonly Type type;
    }
}

