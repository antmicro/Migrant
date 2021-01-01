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
using System.Reflection;
using System.Reflection.Emit;

namespace Migrantoid.Generators
{
    internal class CloneContentMethodGenerator : DynamicReadMethodGenerator<CloneMethodDelegate>
    {
        public CloneContentMethodGenerator(Type t) : base(t, "CloneContent")
        {
            if(t.IsValueType)
            {
                // this method should not be used for value types
                // use reflection-based version instead
                throw new ArgumentException();
            }
        }

        protected override void InnerGenerate(ReaderGenerationContext context)
        {
            foreach(var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                context.Generator.Emit(OpCodes.Ldarg_1);
                context.Generator.Emit(OpCodes.Castclass, type);
                context.Generator.Emit(OpCodes.Ldarg_0);
                context.Generator.Emit(OpCodes.Castclass, type);
                context.Generator.Emit(OpCodes.Ldfld, field);

                if(!field.FieldType.IsValueType)
                {
                    var valuesNotEqualLabel = context.Generator.DefineLabel();
                    
                    context.Generator.Emit(OpCodes.Dup);
                    context.Generator.Emit(OpCodes.Ldarg_0);
                    context.Generator.Emit(OpCodes.Castclass, type);
                    context.Generator.Emit(OpCodes.Bne_Un_S, valuesNotEqualLabel);

                    context.Generator.Emit(OpCodes.Pop);
                    context.Generator.Emit(OpCodes.Ldarg_1);
                    context.Generator.Emit(OpCodes.Castclass, type);

                    context.Generator.MarkLabel(valuesNotEqualLabel);
                }

                context.Generator.Emit(OpCodes.Stfld, field);
            }
        }
    }
}

