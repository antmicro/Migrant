//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.Generators
{
    internal class WriteReferenceMethodGenerator : DynamicWriteMethodGenerator<WriteReferenceMethodDelegate>
    {
        public WriteReferenceMethodGenerator(Type type, bool disableStamping, bool treatCollectionAsUserObject)
            : base(type, "WriteReference", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_0)
        {
        }

        protected override bool InnerGenerate(WriterGenerationContext context)
        {
            var value = new Variable(1);
            WriteMethodGenerator.GenerateWriteDeferredReference(context, value, type);
            return true;
        }
    }
}

