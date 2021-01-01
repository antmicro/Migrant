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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Migrant.Generators
{
    internal class CallPostSerializationHooksMethodGenerator : DynamicWriteMethodGenerator<CallPostSerializationHooksMethodDelegate>
    {
        public CallPostSerializationHooksMethodGenerator(Type type, bool disableStamping, bool treatCollectionAsUserObject) 
            : base(type, "CallPostSerializationHooks", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_0)
        {
        }

        protected override bool InnerGenerate(WriterGenerationContext context)
        {
            var result = false;
            
            var methods = Helpers.GetMethodsWithAttribute(typeof(PostSerializationAttribute), type).ToArray();
            foreach(var method in methods)
            {
                if(!method.IsStatic)
                {
                    context.Generator.Emit(OpCodes.Ldarg_1);
                    context.Generator.Emit(OpCodes.Castclass, method.ReflectedType);
                }
                if(method.IsVirtual)
                {
                    context.Generator.Emit(OpCodes.Callvirt, method);
                }
                else
                {
                    context.Generator.Emit(OpCodes.Call, method);
                }

                result = true;
            }

            methods = Helpers.GetMethodsWithAttribute(typeof(LatePostSerializationAttribute), type).ToArray();
            foreach(var method in methods)
            {
                context.PushObjectWriterOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectWriter, List<Action>>(x => x.postSerializationHooks);

                context.Generator.PushTypeOntoStack(typeof(Action));
                if(method.IsStatic)
                {
                    context.Generator.Emit(OpCodes.Ldnull);
                }
                else
                {
                    context.Generator.Emit(OpCodes.Ldarg_1);
                    context.Generator.Emit(OpCodes.Castclass, method.ReflectedType);
                }

                context.Generator.Emit(OpCodes.Ldtoken, method);
                if(method.DeclaringType.IsGenericType)
                {
                    context.Generator.Emit(OpCodes.Ldtoken, method.DeclaringType);
                    context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, MethodBase>(x => MethodBase.GetMethodFromHandle(new RuntimeMethodHandle(), new RuntimeTypeHandle())));
                }
                else
                {
                    context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, MethodBase>(x => MethodBase.GetMethodFromHandle(new RuntimeMethodHandle())));
                }

                context.Generator.Emit(OpCodes.Castclass, typeof(MethodInfo));
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, Delegate>(x => Delegate.CreateDelegate(null, null, method)));

                context.Generator.Emit(OpCodes.Castclass, typeof(Action));
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<List<Action>>(x => x.Add(null)));

                result = true;
            }

            return result;
        }
    }
}

