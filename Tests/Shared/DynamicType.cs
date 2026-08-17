//
// Copyright (c) 2012-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Antmicro.Migrant.Tests.Shared
{
    public class DynamicType
    {
        public static DynamicType CreateClass(string name, DynamicType baseClass = null, DynamicType genericArgument = null, IEnumerable<DynamicType> additionalTypes = null)
        {
            var result = new DynamicType(KindOfDynamicType.Class);
            result.name = name;
            result.baseClass = baseClass;
            result.genericArgument = genericArgument;
            result.additionalTypes = additionalTypes;
            return result;
        }

        public static DynamicType CreateStruct(string name)
        {
            var result = new DynamicType(KindOfDynamicType.Struct);
            result.name = name;
            result.baseClass = null;
            return result;
        }

        public static DynamicType CreateInterface(string name)
        {
            var result = new DynamicType(KindOfDynamicType.Interface);
            result.name = name;
            result.baseClass = null;
            return result;
        }

        public DynamicType WithField(string name, DynamicType type)
        {
            fields.Add(name, new FieldDescriptor { DynamicType = type });
            return this;
        }

        public DynamicType WithField(string name, Type type)
        {
            fields.Add(name, new FieldDescriptor { Type = type });
            return this;
        }

        public DynamicType WithField<T>(string name)
        {
            return WithField(name, typeof(T));
        }

        public DynamicType WithTransientField(string name, Type type)
        {
            fields.Add(name, new FieldDescriptor { Type = type, IsTransient = true });
            return this;
        }

        public DynamicType WithConstructorField<T>(string name)
        {
            return WithConstructorField(name, typeof(T));
        }

        public DynamicType WithConstructorField(string name, Type type)
        {
            fields.Add(name, new FieldDescriptor { Type = type, IsConstructor = true });
            return this;
        }

        public Type CreateType(AssemblyBuilder assemblyBuilder, string moduleName)
        {
            var module = new DynamicModule(assemblyBuilder.DefineDynamicModule(moduleName));
            if(additionalTypes != null)
            {
                foreach(var additionalType in additionalTypes)
                {
                    additionalType.InnerCreateType(module);
                }
            }
            return InnerCreateType(module);
        }

        private Type InnerCreateType(DynamicModule module)
        {
            Type result;
            if(module.CreatedTypes.TryGetValue(name, out result))
            {
                return result;
            }

            TypeBuilder typeBuilder = null;
            switch(type)
            {
            case KindOfDynamicType.Struct:
                    typeBuilder = module.Builder.DefineType(name,
                        TypeAttributes.Public |
                        TypeAttributes.Sealed |
                        TypeAttributes.SequentialLayout,
                        typeof(ValueType));
                break;
            case KindOfDynamicType.Interface:
                typeBuilder = module.Builder.DefineType(name, TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract);
                break;
            case KindOfDynamicType.Class:
                typeBuilder = module.Builder.DefineType(name, TypeAttributes.Class | TypeAttributes.Public);
                if(genericArgument != null)
                {
                    typeBuilder.DefineGenericParameters(new [] { "TFirst" });
                }

                break;
            }

            if(baseClass != null)
            {
                typeBuilder.SetParent(baseClass.InnerCreateType(module));
            }

            foreach(var field in fields)
            {
                if(field.Value.DynamicType != null && field.Value.Type == null)
                {
                    field.Value.Type = field.Value.DynamicType.InnerCreateType(module);
                }

                var fBldr = typeBuilder.DefineField(field.Key, field.Value.Type, FieldAttributes.Public);
                if(field.Value.IsTransient)
                {
                    var taC = typeof(TransientAttribute).GetConstructor(System.Type.EmptyTypes);
                    fBldr.SetCustomAttribute(new CustomAttributeBuilder(taC, new object[0]));
                }
                if(field.Value.IsConstructor)
                {
                    var taC = typeof(ConstructorAttribute).GetConstructors()[0];
                    fBldr.SetCustomAttribute(new CustomAttributeBuilder(taC, new object[] { new object[0] }));
                }
            }

            result = typeBuilder.CreateType();
            if(genericArgument != null)
            {
                result = result.MakeGenericType(genericArgument.InnerCreateType(module));
            }
            module.CreatedTypes[name] = result;
            return result;
        }

        public object Instantiate(Version version = null)
        {
            // Caller assembly must be obtained here.
            // If we do it inside nested call, we get a handle to this assembly instead.
            // https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly.getcallingassembly#remarks
            var callerAssembly = Assembly.GetCallingAssembly();
            return BuildAssembly(version, callerAssembly);
        }

#if NET9_0_OR_GREATER
        private object BuildAssembly(Version version, Assembly callerAssembly)
        {
            var dllName = $"{AssemblyName.Name}.dll";
            var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(dllName)) { Version = version };
            var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
            var dynamicType = CreateType(assemblyBuilder, dllName);
            if(dynamicType == null)
            {
                throw new InvalidOperationException("Could not create type.");
            }

            using var stream = new MemoryStream();
            assemblyBuilder.Save(stream);
            stream.Position = 0;

            var currentAlc = AssemblyLoadContext.GetLoadContext(callerAssembly);

            // Multiple assemblies with the same name can't be loaded into the same domain,
            // but change to the the assembly name makes inner types 'different'.
            // Loading assemblies into temporary domains avoids collisions thus
            // if we are not already in a custom domain one will be created.
            if (currentAlc == AssemblyLoadContext.Default)
            {
                currentAlc = new AssemblyLoadContext($"TemporaryDomain_{Guid.NewGuid()}", isCollectible: true);
            }

            var staticAssembly = currentAlc.LoadFromStream(stream);
            var builtType = staticAssembly.GetType(dynamicType.FullName);

            var result = Activator.CreateInstance(builtType);
            foreach(var field in fields.Where(x => x.Value.DynamicType != null))
            {
                var runtimeField = builtType.GetField(field.Key);
                var fieldValue = Activator.CreateInstance(runtimeField.FieldType);
                runtimeField.SetValue(result, fieldValue);
            }

            return result;
        }
#else
        private object BuildAssembly(Version version, Assembly callerAssembly)
        {
            var dllName = $"{AssemblyName.Name}.dll";
            var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(dllName)) { Version = version };
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var builtType = CreateType(assemblyBuilder, dllName);
            if(builtType == null)
            {
                throw new InvalidOperationException("Could not create type.");
            }

            var result = Activator.CreateInstance(builtType);
            foreach(var field in fields.Where(x => x.Value.DynamicType != null))
            {
                builtType.GetField(field.Key).SetValue(result, Activator.CreateInstance(field.Value.Type));
            }

            return result;
        }
#endif

        private DynamicType(KindOfDynamicType type)
        {
            this.type = type;
            fields = new Dictionary<string, FieldDescriptor>();
        }

        private readonly KindOfDynamicType type;
        private string name;
        private readonly Dictionary<string, FieldDescriptor> fields;
        private DynamicType baseClass;
        private DynamicType genericArgument;

        private IEnumerable<DynamicType> additionalTypes;
        private static readonly AssemblyName AssemblyName = new AssemblyName("TestAssembly");

        public static string Prefix;

        private class FieldDescriptor
        {
            public DynamicType DynamicType { get; set; }

            public Type Type { get; set; }

            public bool IsTransient { get; set; }

            public bool IsConstructor { get; set; }
        }

        private enum KindOfDynamicType
        {
            Class,
            Struct,
            Interface
        }

        private class DynamicModule
        {
            public DynamicModule(ModuleBuilder builder)
            {
                Builder = builder;
                CreatedTypes = new Dictionary<string, Type>();
            }

            public ModuleBuilder Builder { get; private set; }
            public Dictionary<string, Type> CreatedTypes { get; private set; }
        }
    }
}
