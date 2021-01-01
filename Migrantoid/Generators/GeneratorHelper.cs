/*
  Copyright (c) 2013-2016 Ant Micro <www.antmicro.com>

  Authors:
   * Mateusz Holenko (mholenko@antmicro.com)

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant.Generators
{
    internal static class GeneratorHelper
    {
        public static void GenerateLoop(GenerationContextBase context, LocalBuilder countLocal, Action<LocalBuilder> loopAction, bool reversed = false)
        {
            var loopControlLocal = context.Generator.DeclareLocal(typeof(int));
            GenerateLoop(context, countLocal, loopControlLocal, () => loopAction(loopControlLocal), reversed);
        }

        public static void GenerateLoop(GenerationContextBase context, LocalBuilder countLocal, LocalBuilder loopControlLocal, Action loopAction, bool reversed = false)
        {
            var loopLabel = context.Generator.DefineLabel();
            var loopFinishLabel = context.Generator.DefineLabel();

            if(reversed)
            {
                context.Generator.PushLocalValueOntoStack(countLocal);
                context.Generator.PushIntegerOntoStack(1);
                context.Generator.Emit(OpCodes.Sub); // put <<countLocal>> - 1 on stack
            }
            else
            {
                context.Generator.PushIntegerOntoStack(0); // put <<0> on stack
            }

            context.Generator.StoreLocalValueFromStack(loopControlLocal); // initialize <<loopControl>> variable using value from stack

            context.Generator.MarkLabel(loopLabel);
            context.Generator.PushLocalValueOntoStack(loopControlLocal);

            if(reversed)
            {
                context.Generator.PushIntegerOntoStack(-1);
            }
            else
            {
                context.Generator.PushLocalValueOntoStack(countLocal);
            }
            context.Generator.Emit(OpCodes.Beq, loopFinishLabel);

            loopAction();

            context.Generator.PushLocalValueOntoStack(loopControlLocal);
            context.Generator.PushIntegerOntoStack(reversed ? -1 : 1);
            context.Generator.Emit(OpCodes.Add);
            context.Generator.StoreLocalValueFromStack(loopControlLocal); // change <<loopControl>> variable by one

            context.Generator.Emit(OpCodes.Br, loopLabel); // jump to the next loop iteration

            context.Generator.MarkLabel(loopFinishLabel);
        }

        public static void GenerateCodeCall<T1>(this ILGenerator generator, Action<T1> a)
        {
            generator.Emit(OpCodes.Call, a.Method);
        }

        public static void GenerateCodeCall<T1, T2>(this ILGenerator generator, Action<T1, T2> a)
        {
            generator.Emit(OpCodes.Call, a.Method);
        }

        public static void GenerateCodeCall<T1, T2, T3>(this ILGenerator generator, Action<T1, T2, T3> a)
        {
            generator.Emit(OpCodes.Call, a.Method);
        }

        public static void GenerateCodeCall<T1, T2, T3, T4, T5>(this ILGenerator generator, Action<T1, T2, T3, T4, T5> a)
        {
            generator.Emit(OpCodes.Call, a.Method);
        }

        public static void GenerateCodeFCall<T1, TResult>(this ILGenerator generator, Func<T1, TResult> f)
        {
            generator.Emit(OpCodes.Call, f.Method);
        }

        public static void GenerateCodeFCall<T1, T2, T3, TResult>(this ILGenerator generator, Func<T1, T2, T3, TResult> f)
        {
            generator.Emit(OpCodes.Call, f.Method);
        }

        public static void Call(this ILGenerator generator, Expression<Action> expression)
        {
            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(expression));
        }

        public static void Call<T>(this ILGenerator generator, Expression<Action<T>> expression)
        {
            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(expression));
        }

        public static void Callvirt(this ILGenerator generator, Expression<Action> expression)
        {
            generator.Emit(OpCodes.Callvirt, Helpers.GetMethodInfo(expression));
        }

        public static void Callvirt<T>(this ILGenerator generator, Expression<Action<T>> expression)
        {
            generator.Emit(OpCodes.Callvirt, Helpers.GetMethodInfo(expression));
        }

        public static void PushPropertyValueOntoStack<T, TResult>(this ILGenerator generator, Expression<Func<T, TResult>> expression)
        {
            generator.Emit(OpCodes.Call, Helpers.GetPropertyGetterInfo(expression));
        }

        public static void PushFieldValueOntoStack<T, TResult>(this ILGenerator generator, Expression<Func<T, TResult>> expression)
        {
            generator.Emit(OpCodes.Ldfld, Helpers.GetFieldInfo(expression));
        }

        public static void PushFieldInfoOntoStack(this ILGenerator generator, FieldInfo finfo)
        {
            generator.Emit(OpCodes.Ldtoken, finfo);
            if(finfo.DeclaringType.IsGenericType)
            {
                generator.Emit(OpCodes.Ldtoken, finfo.ReflectedType);
                generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, object>(x => FieldInfo.GetFieldFromHandle(finfo.FieldHandle, new RuntimeTypeHandle())));
            }
            else
            {
                generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, object>(x => FieldInfo.GetFieldFromHandle(finfo.FieldHandle)));
            }
        }

        public static void PushTypeOntoStack(this ILGenerator generator, Type type)
        {
            generator.Emit(OpCodes.Ldtoken, type);
            generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<RuntimeTypeHandle, Type>(o => Type.GetTypeFromHandle(o))); // loads value of <<typeToGenerate>> onto stack
        }

        public static void PushLocalValueOntoStack(this ILGenerator generator, LocalBuilder local)
        {
            switch(local.LocalIndex)
            {
            case 0:
                generator.Emit(OpCodes.Ldloc_0);
                break;
            case 1:
                generator.Emit(OpCodes.Ldloc_1);
                break;
            case 2:
                generator.Emit(OpCodes.Ldloc_2);
                break;
            case 3:
                generator.Emit(OpCodes.Ldloc_3);
                break;
            default:
                if(local.LocalIndex < 256)
                {
                    generator.Emit(OpCodes.Ldloc_S, (byte)local.LocalIndex);
                }
                else
                {
                    generator.Emit(OpCodes.Ldloc, local);
                }
                break;
            }
        }

        public static void PushLocalAddressOntoStack(this ILGenerator generator, LocalBuilder local)
        {
            if(local.LocalIndex < 256)
            {
                generator.Emit(OpCodes.Ldloca_S, (byte)local.LocalIndex);
            }
            else
            {
                generator.Emit(OpCodes.Ldloca, local);
            }
        }

        public static void StoreLocalValueFromStack(this ILGenerator generator, LocalBuilder local)
        {
            switch(local.LocalIndex)
            {
            case 0:
                generator.Emit(OpCodes.Stloc_0);
                break;
            case 1:
                generator.Emit(OpCodes.Stloc_1);
                break;
            case 2:
                generator.Emit(OpCodes.Stloc_2);
                break;
            case 3:
                generator.Emit(OpCodes.Stloc_3);
                break;
            default:
                if(local.LocalIndex < 256)
                {
                    generator.Emit(OpCodes.Stloc_S, (byte)local.LocalIndex);
                }
                else
                {
                    generator.Emit(OpCodes.Stloc, local);
                }
                break;
            }
        }

        public static void PushIntegerOntoStack(this ILGenerator generator, int value)
        {
            switch(value)
            {
            case -1:
                generator.Emit(OpCodes.Ldc_I4_M1);
                break;
            case 0:
                generator.Emit(OpCodes.Ldc_I4_0);
                break;
            case 1:
                generator.Emit(OpCodes.Ldc_I4_1);
                break;
            case 2:
                generator.Emit(OpCodes.Ldc_I4_2);
                break;
            case 3:
                generator.Emit(OpCodes.Ldc_I4_3);
                break;
            case 4:
                generator.Emit(OpCodes.Ldc_I4_4);
                break;
            case 5:
                generator.Emit(OpCodes.Ldc_I4_5);
                break;
            case 6:
                generator.Emit(OpCodes.Ldc_I4_6);
                break;
            case 7:
                generator.Emit(OpCodes.Ldc_I4_7);
                break;
            case 8:
                generator.Emit(OpCodes.Ldc_I4_8);
                break;
            default:
                if(value > -129 && value < 128)
                {
                    generator.Emit(OpCodes.Ldc_I4_S, value);
                }
                else
                {
                    generator.Emit(OpCodes.Ldc_I4, value);
                }
                break;
            }
        }

        public static void PushVariableOntoStack(this ILGenerator generator, Variable var)
        {
            var.PushValueOntoStack(generator);
        }

        public static void PushVariableAddressOntoStack(this ILGenerator generator, Variable var)
        {
            var.PushAddressOntoStack(generator);
        }

        public static void StoreVariableValueFromStack(this ILGenerator generator, Variable var)
        {
            var.StoreValueFromStack(generator);
        }

        [Conditional("DEBUG")]
        public static void Debug_PrintValueFromLocal<T>(this ILGenerator generator, LocalBuilder local, string message = null)
        {
            generator.Emit(OpCodes.Ldloc, local);
            generator.Debug_PrintValueFromStack<T>(message);
        }

        [Conditional("DEBUG")]
        public static void Debug_PrintValueFromStack<T>(this ILGenerator generator, string message = null)
        {
            generator.Emit(OpCodes.Ldstr, message ?? "DEBUG");
            GenerateCodeCall<T, string>(generator, (v, m) =>
            {
                Console.WriteLine("{0}: {1}", m, v);
            });
        }
    }
}

