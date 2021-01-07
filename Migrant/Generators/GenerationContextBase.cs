//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection.Emit;

namespace Antmicro.Migrant.Generators
{
    internal abstract class GenerationContextBase
    {
        public GenerationContextBase(ILGenerator generator, bool disableStamping, bool treatCollectionAsUserObject)
        {
            this.generator = generator;

            DisableStamping = disableStamping;
            TreatCollectionAsUserObject = treatCollectionAsUserObject;
        }

        public abstract GenerationContextBase WithGenerator(ILGenerator g);

        protected void CheckAndEmit(OpCode? argument)
        {
            if(!argument.HasValue)
            {
                throw new InvalidOperationException();
            }

            generator.Emit(argument.Value);
        }

        public bool DisableStamping { get; private set; }
        public bool TreatCollectionAsUserObject { get; private set; }
        public ILGenerator Generator { get { return generator; } }

        protected readonly ILGenerator generator;
    }
}

