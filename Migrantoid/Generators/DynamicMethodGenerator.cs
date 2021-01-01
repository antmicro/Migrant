// *******************************************************************
//
//  Copyright (c) 2012-2016, Antmicro Ltd <antmicro.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// *******************************************************************
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

