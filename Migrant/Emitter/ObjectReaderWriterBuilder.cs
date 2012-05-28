using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading;
using System.Reflection;
using AntMicro.Migrant;

namespace AntMicro.Migrant.Emitter
{
	public class ObjectReaderWriterBuilder
	{
		public ObjectReaderWriterBuilder(Type[] typeArray)
		{
			this.typeArray = (Type[])typeArray.Clone();
			typeIndices = new Dictionary<Type, int>();
			FillTypeIndices();
		}

		public void Build()
		{
			if(built)
			{
				return;
			}
			var assemblyName = new AssemblyName("GeneratedSerializer");
			var assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Save);
		    moduleBuilder = assemblyBuilder.DefineDynamicModule("GeneratedSerializerModule", "GeneratedSerializer.dll");
			BuildObjectWriter();
			assemblyBuilder.Save("GeneratedSerializer.dll");
			built = true;
		}

		private void BuildObjectWriter()
		{
			 // these are fake variables only used to enable getting instance methods with expression trees
			ObjectWriter baseWriter = null;
			Type nullType = null;

			var writer = moduleBuilder.DefineType("GeneratedObjectWriter", TypeAttributes.Class | TypeAttributes.Public, typeof(ObjectWriter));
			CopyFirstCtorFromBaseClass(writer);
			var writeObjectInner = OverrideMethod(typeof(ObjectWriter).GetMethod("WriteObjectInner", BindingFlags.Instance | BindingFlags.NonPublic), writer);

			var generator = writeObjectInner.GetILGenerator();
			// TODO: preserialization hook

			// first we need to get the type id for the object
			// dictionary for getting type id
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => GetType()));
			generator.DeclareLocal(typeof(Type));
			generator.Emit(OpCodes.Dup); // TODO
			generator.Emit(OpCodes.Stloc_0);
			generator.Emit(OpCodes.Call, Helpers.GetMethodInfo(() => baseWriter.TouchType(nullType)));

			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldfld, typeof(ObjectWriter).GetField("TypeIndices", BindingFlags.NonPublic | BindingFlags.Instance)); // TODO: expr trees?
			generator.Emit(OpCodes.Ldloc_0);
			generator.Emit(OpCodes.Call, typeof(IDictionary<Type, int>).GetProperty("Item").GetGetMethod());

			// the jump table will be prepared now
			// fortunately, our indices are consequent and we know how many are there
			var numberOfKnownTypes = typeArray.Length;
			var destinations = new Label[numberOfKnownTypes];
			for(var i = 0; i < destinations.Length; i++)
			{
				destinations[i] = generator.DefineLabel();
			}
			generator.Emit(OpCodes.Switch, destinations);

			// TODO: write the int i - type mark

			// we land in default case
			generator.Emit(OpCodes.Ldstr, "DEFAULT");
			generator.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new [] { typeof(string) }));
			generator.Emit(OpCodes.Ret);

			for(var i = 0; i < destinations.Length; i++)
			{
				GenerateCase(generator, i, destinations[i]);
			}
        	generator.Emit(OpCodes.Ret); // TODO: needed?

			writer.CreateType(); // TODO: needed?
		}

		private void GenerateCase(ILGenerator generator, int i, Label label)
		{
			var type = typeArray[i];
			generator.MarkLabel(label);
			var fields = GetAllFields().Where(Helpers.IsNotTransient).OrderBy(x => x.Name); // TODO: unify
			generator.Emit(OpCodes.Ldarg_1); // object to serialize
			foreach(var field in fields)
			{

			}
			generator.Emit(OpCodes.Ret);
		}

		private void GenerateWriteFields(ILGenerator, Type type)
		{

		}

		private void FillTypeIndices()
		{
			for(var i = 0; i < typeArray.Length; i++)
			{
				typeIndices.Add(typeArray[i], i);
			}
		}

		private static void CopyFirstCtorFromBaseClass(TypeBuilder builder)
		{
			var baseCtor = builder.BaseType.GetConstructors()[0];
			var parameterTypes = baseCtor.GetParameters().Select(x => x.ParameterType).ToArray();
			var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parameterTypes);
			var generator = ctor.GetILGenerator();
			var numberOfArgs = parameterTypes.Length;
			for(var i = 0; i <= numberOfArgs; i++)
			{
				generator.Emit(OpCodes.Ldarg_S, i);
			}
			generator.Emit(OpCodes.Call, baseCtor);
			generator.Emit(OpCodes.Ret);
		}

		private static MethodBuilder OverrideMethod(MethodInfo overridenMethod, TypeBuilder destinationType)
		{
			var parameterTypes = overridenMethod.GetParameters().Select(x => x.ParameterType).ToArray();
			var derivedMethod = destinationType.DefineMethod(overridenMethod.Name, MethodAttributes.Family | MethodAttributes.Virtual,
			                                                 overridenMethod.ReturnType, parameterTypes);
			destinationType.DefineMethodOverride(derivedMethod, overridenMethod);
			return derivedMethod;
		}

		private bool built;
		private ModuleBuilder moduleBuilder;
		private readonly Type[] typeArray;
		private readonly Dictionary<Type, int> typeIndices;
	}
}

