//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection.Emit;
using Antmicro.Migrant.Generators;

namespace Antmicro.Migrant.Utilities
{
    internal class Variable
    {
        public Variable(int argumentId, Type type = null)
        {
            OpCode argOpCode;
            switch(argumentId)
            {
            case 0:
                argOpCode = OpCodes.Ldarg_0;
                break;
            case 1:
                argOpCode = OpCodes.Ldarg_1;
                break;
            case 2:
                argOpCode = OpCodes.Ldarg_2;
                break;
            case 3:
                argOpCode = OpCodes.Ldarg_3;
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }

            if(type != null)
            {
                pushValueAction = g =>
                {
                    g.Emit(argOpCode);
                    g.Emit(OpCodes.Castclass, type);
                };
            }
            else
            {
                pushValueAction = g => g.Emit(argOpCode);
            }

            storeValueAction = g => g.Emit(OpCodes.Starg_S, argumentId);
            pushAddressAction = g => g.Emit(OpCodes.Ldarga_S, argumentId);
        }

        public Variable(LocalBuilder local)
        {
            pushValueAction = g => g.PushLocalValueOntoStack(local);
            pushAddressAction = g => g.PushLocalAddressOntoStack(local);
            storeValueAction = g => g.StoreLocalValueFromStack(local);
        }

        public void PushValueOntoStack(ILGenerator generator)
        {
            pushValueAction(generator);
        }

        public void PushAddressOntoStack(ILGenerator generator)
        {
            pushAddressAction(generator);
        }

        public void StoreValueFromStack(ILGenerator generator)
        {
            if(storeValueAction == null)
            {
                throw new InvalidOperationException();
            }

            storeValueAction(generator);
        }

        private readonly Action<ILGenerator> pushValueAction;
        private readonly Action<ILGenerator> pushAddressAction;
        private readonly Action<ILGenerator> storeValueAction;
    }
}

