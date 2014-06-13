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
using System.Runtime.Serialization;
using System.Collections.Generic;

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
            fields.Add(name, Tuple.Create(type, false));
            return this;
        }

        public DynamicClass WithField<T>(string name)
        {
            return WithField(name, typeof(T));
        }

        public DynamicClass WithTransientField(string name, Type type)
        {
            fields.Add(name, Tuple.Create(type, true));
            return this;
        }

        public Type CreateType(AssemblyBuilder assemblyBuilder)
        {
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(AssemblyName.Name + ".dll");
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
                var fBldr = typeBuilder.DefineField(field.Key, field.Value.Item1, FieldAttributes.Public);
                if (field.Value.Item2)
                {
                    var taC = typeof(TransientAttribute).GetConstructor(Type.EmptyTypes);
                    fBldr.SetCustomAttribute(new CustomAttributeBuilder(taC, new object[0]));
                }
            }

            return typeBuilder.CreateType();
        }

        public object Instantiate()
        {
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(AssemblyName, /*persistent ?*/ AssemblyBuilderAccess.RunAndSave /*: AssemblyBuilderAccess.Run*/);
            var builtType = CreateType(assemblyBuilder);
            assemblyBuilder.Save(AssemblyName.Name + ".dll");
            return Activator.CreateInstance(builtType);
        }

        private DynamicClass()
        {
        }

        private string name;
        private Dictionary<string, Tuple<Type, bool>> fields = new Dictionary<string, Tuple<Type, bool>>();
        private DynamicClass baseClass;

        private static readonly AssemblyName AssemblyName = new AssemblyName("TestAssembly");
    }
}
