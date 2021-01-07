//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Antmicro.Migrant.Generators
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

