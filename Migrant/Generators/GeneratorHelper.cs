using System;
using System.Reflection.Emit;

namespace AntMicro.Migrant.Generators
{
	public class GeneratorHelper
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

