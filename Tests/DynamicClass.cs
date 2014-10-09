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

namespace Antmicro.Migrant.Tests
{
    [Serializable]
    public class DynamicClass
    {
        public static DynamicClass Create(string name, DynamicClass baseClass = null)
        {
            var result = new DynamicClass();
            result.name = name;
            result.baseClass = baseClass;
            return result;
        }

        public DynamicClass WithField(string name, Type type)
        {
            fields.Add(name, new FieldDescriptor { Type = type });
            return this;
        }

        public DynamicClass WithField<T>(string name)
        {
            return WithField(name, typeof(T));
        }

        public DynamicClass WithTransientField(string name, Type type)
        {
            fields.Add(name, new FieldDescriptor { Type = type, IsTransient = true });
            return this;
        }

        public DynamicClass WithConstructorField<T>(string name)
        {
            return WithConstructorField(name, typeof(T));
        }

        public DynamicClass WithConstructorField(string name, Type type)
        {
            fields.Add(name, new FieldDescriptor { Type = type, IsConstructor = true });
            return this;
        }

        public Type CreateType(AssemblyBuilder assemblyBuilder, string moduleName)
        {
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(moduleName);
            return InnerCreateType(moduleBuilder);
        }

        private Type InnerCreateType(ModuleBuilder moduleBuilder)
        {
            var typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Class | TypeAttributes.Public);
            if(baseClass != null)
            {
                typeBuilder.SetParent(baseClass.InnerCreateType(moduleBuilder));
            }

            foreach(var field in fields)
            {
                var fBldr = typeBuilder.DefineField(field.Key, field.Value.Type, FieldAttributes.Public);
                if (field.Value.IsTransient)
                {
                    var taC = typeof(TransientAttribute).GetConstructor(Type.EmptyTypes);
                    fBldr.SetCustomAttribute(new CustomAttributeBuilder(taC, new object[0]));
                }
                if(field.Value.IsConstructor)
                {
                    var taC = typeof(ConstructorAttribute).GetConstructors()[0];
                    fBldr.SetCustomAttribute(new CustomAttributeBuilder(taC, new object[] { new object[0] }));
                }
            }

            return typeBuilder.CreateType();
        }

        public object Instantiate()
        {
            var dllName = string.Format("{0}-{1}-{2}.dll", AssemblyName.Name, "xxx", counter);

            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(string.Format("{0}-{1}-{2}", AssemblyName.Name, "xxx", counter/*++*/)), /*persistent ?*/ AssemblyBuilderAccess.RunAndSave /*: AssemblyBuilderAccess.Run*/);
            var builtType = CreateType(assemblyBuilder, dllName);
            assemblyBuilder.Save(dllName);
            if(!string.IsNullOrWhiteSpace(prefix))
            {
                File.Delete(Path.Combine(prefix, dllName));
                File.Move(dllName, Path.Combine(prefix, dllName));
            }
            return Activator.CreateInstance(builtType);
        }

        private DynamicClass()
        {
        }

        private string name;
        private Dictionary<string, FieldDescriptor> fields = new Dictionary<string, FieldDescriptor>();
        private DynamicClass baseClass;

        private static readonly AssemblyName AssemblyName = new AssemblyName("TestAssembly");
        private const int counter = 0;
        public static string prefix;

        [Serializable]
        private class FieldDescriptor
        {
            public Type Type { get; set; }
            public bool IsTransient { get; set; }
            public bool IsConstructor { get; set; }
        }
    }
}
