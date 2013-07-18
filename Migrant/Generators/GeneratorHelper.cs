/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

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
using System.Reflection.Emit;

namespace AntMicro.Migrant.Generators
{
	internal class GeneratorHelper
	{
		public static void GenerateLoop(ILGenerator generator, LocalBuilder countLocal, Action<LocalBuilder> loopAction, bool reversed = false)
		{
			var loopControlLocal = generator.DeclareLocal(typeof(Int32));

			var loopLabel = generator.DefineLabel();
			var loopFinishLabel = generator.DefineLabel();

			if(reversed)
			{
				generator.Emit(OpCodes.Ldloc, countLocal);
				generator.Emit(OpCodes.Ldc_I4_1);
				generator.Emit(OpCodes.Sub); // put <<countLocal>> - 1 on stack
			}
			else
			{
				generator.Emit(OpCodes.Ldc_I4_0); // put <<0> on stack
			}

			generator.Emit(OpCodes.Stloc, loopControlLocal); // initialize <<loopControl>> variable using value from stack

			generator.MarkLabel(loopLabel);
			generator.Emit(OpCodes.Ldloc, loopControlLocal);

			if(reversed)
			{
				generator.Emit(OpCodes.Ldc_I4, -1);
			}
			else
			{
				generator.Emit(OpCodes.Ldloc, countLocal);

			}
			generator.Emit(OpCodes.Beq, loopFinishLabel);

			loopAction(loopControlLocal);

			generator.Emit(OpCodes.Ldloc, loopControlLocal);
			generator.Emit(OpCodes.Ldc_I4, reversed ? 1 : -1);
			generator.Emit(OpCodes.Sub);
			generator.Emit(OpCodes.Stloc, loopControlLocal); // change <<loopControl>> variable by one
			generator.Emit(OpCodes.Br, loopLabel); // jump to the next loop iteration

			generator.MarkLabel(loopFinishLabel);
		}

		public static void GenerateCodeCall<T1, T2, T3>(ILGenerator generator, Action<T1, T2, T3> a)
		{
			generator.Emit(OpCodes.Call, a.Method);
		}

		private GeneratorHelper()
		{
		}
	}
}

