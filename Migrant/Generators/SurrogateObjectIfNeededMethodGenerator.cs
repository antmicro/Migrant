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
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.Generators
{
    internal class SurrogateObjectIfNeededMethodGenerator : DynamicWriteMethodGenerator<SurrogateObjectIfNeededDelegate>
    {
        public SurrogateObjectIfNeededMethodGenerator(Type type, SwapList surrogatesForObjects, bool disableStamping, bool treatCollectionAsUserObject) 
            : base(type, "SurrogateObjectIfNeeded", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_0)
        {
            this.surrogatesForObjects = surrogatesForObjects;
        }

        protected override bool InnerGenerate(WriterGenerationContext context)
        {
            var surrogateId = surrogatesForObjects.FindMatchingIndex(type);
            if(surrogateId == -1)
            {
                return false;
            }

            var objVariable = new Variable(1);

            context.PushObjectWriterOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectWriter, SwapList>(x => x.surrogatesForObjects);
            context.Generator.PushIntegerOntoStack(surrogateId);
            context.Generator.Call<SwapList>(x => x.GetByIndex(0));

            // call surrogate factory to obtain surrogate object
            var delegateType = typeof(Func<,>).MakeGenericType(type, typeof(object));
            context.Generator.Emit(OpCodes.Castclass, delegateType);
            context.Generator.PushVariableOntoStack(objVariable);
            if(type.IsValueType)
            {
                context.Generator.Emit(OpCodes.Unbox_Any, type);
            }
            context.Generator.Emit(OpCodes.Call, delegateType.GetMethod("Invoke"));
            context.Generator.StoreVariableValueFromStack(objVariable);

            context.PushObjectWriterOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectWriter, ObjectIdentifier>(x => x.identifier);
            context.Generator.PushVariableOntoStack(objVariable); // object reference
            context.Generator.Emit(OpCodes.Ldarg_2); // reference id
            context.Generator.Call<ObjectIdentifier>(x => x.SetIdentifierForObject(null, 0));

            context.Generator.PushVariableOntoStack(objVariable);

            return true;
        }

        private readonly SwapList surrogatesForObjects;
    }
}

