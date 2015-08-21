// *******************************************************************
//
//  Copyright (c) 2011-2014, Antmicro Ltd <antmicro.com>
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
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Antmicro.Migrant.Tests
{
    [Serializable]
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
                        TypeAttributes.SequentialLayout |
                        TypeAttributes.Serializable,
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
            var dllName = string.Format("{0}-{1}-{2}.dll", AssemblyName.Name, "xxx", "0");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName(string.Format("{0}-{1}-{2}", AssemblyName.Name, "xxx", counter)) { Version = version }, 
                AssemblyBuilderAccess.RunAndSave);
            var builtType = CreateType(assemblyBuilder, dllName);
            assemblyBuilder.Save(dllName);
            if(!string.IsNullOrWhiteSpace(prefix))
            {
                File.Delete(Path.Combine(prefix, dllName));
                File.Move(dllName, Path.Combine(prefix, dllName));
            }
            var result = Activator.CreateInstance(builtType);
            foreach(var field in fields.Where(x => x.Value.DynamicType != null))
            {
                builtType.GetField(field.Key).SetValue(result, Activator.CreateInstance(field.Value.Type));
            }
            return result;
        }

        private DynamicType(KindOfDynamicType type)
        {
            this.type = type;
            fields = new Dictionary<string, FieldDescriptor>();
        }

        private KindOfDynamicType type;
        private string name;
        private Dictionary<string, FieldDescriptor> fields;
        private DynamicType baseClass;
        private DynamicType genericArgument;

        private IEnumerable<DynamicType> additionalTypes;
        private static readonly AssemblyName AssemblyName = new AssemblyName("TestAssembly");
        private const int counter = 0;
        public static string prefix;

        [Serializable]
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
