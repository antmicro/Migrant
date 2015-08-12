// *******************************************************************
//
//  Copyright (c) 2012-2014, Antmicro Ltd <antmicro.com>
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
using Antmicro.Migrant.Utilities;
using System.Reflection;
using System.Linq;

namespace Antmicro.Migrant.VersionTolerance
{
    internal class MethodDescriptor : IIdentifiedElement
    {
        public MethodDescriptor()
        {
        }

        public MethodDescriptor(MethodInfo method)
        {
            UnderlyingMethod = method;
        }

        public void WriteTo(ObjectWriter writer)
        {
            writer.Types.TouchAndWriteId(UnderlyingMethod.ReflectedType);

            var methodParameters = UnderlyingMethod.GetParameters();
            if(UnderlyingMethod.IsGenericMethod)
            {
                var genericDefinition = UnderlyingMethod.GetGenericMethodDefinition();
                var genericArguments = UnderlyingMethod.GetGenericArguments();
                var genericMethodParamters = genericDefinition.GetParameters();

                writer.PrimitiveWriter.Write(genericDefinition.Name);
                writer.PrimitiveWriter.Write(genericArguments.Length);
                for(int i = 0; i < genericArguments.Length; i++)
                {
                    writer.Types.TouchAndWriteId(genericArguments[i]);
                }

                writer.PrimitiveWriter.Write(genericMethodParamters.Length);
                for(int i = 0; i < genericMethodParamters.Length; i++)
                {
                    writer.PrimitiveWriter.Write(genericMethodParamters[i].ParameterType.IsGenericParameter);
                    if(genericMethodParamters[i].ParameterType.IsGenericParameter)
                    {
                        writer.PrimitiveWriter.Write(genericMethodParamters[i].ParameterType.GenericParameterPosition);
                    }
                    else
                    {
                        writer.Types.TouchAndWriteId(methodParameters[i].ParameterType);
                    }
                }
            }
            else
            {
                writer.PrimitiveWriter.Write(UnderlyingMethod.Name);
                writer.PrimitiveWriter.Write(0); // no generic arguments
                writer.PrimitiveWriter.Write(methodParameters.Length);

                foreach(var p in methodParameters)
                {
                    writer.Types.TouchAndWriteId(p.ParameterType);
                }
            }
        }

        public void ReadFromStream(ObjectReader reader)
        {
            var type = reader.Types.Read().UnderlyingType;
            var methodName = reader.PrimitiveReader.ReadString();
            var genericArgumentsCount = reader.PrimitiveReader.ReadInt32();
            var genericArguments = new Type[genericArgumentsCount];
            for(int i = 0; i < genericArgumentsCount; i++)
            {
                genericArguments[i] = reader.Types.Read().UnderlyingType;
            }

            var parametersCount = reader.PrimitiveReader.ReadInt32();
            if(genericArgumentsCount > 0)
            {
                var parameters = new TypeOrGenericTypeArgument[parametersCount];
                for(int i = 0; i < parameters.Length; i++)
                {
                    var genericType = reader.PrimitiveReader.ReadBoolean();
                    parameters[i] = genericType ? 
                        new TypeOrGenericTypeArgument(reader.PrimitiveReader.ReadInt32()) :
                        new TypeOrGenericTypeArgument(reader.Types.Read().UnderlyingType);
                }

                UnderlyingMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SingleOrDefault(m => 
                    m.IsGenericMethod && 
                    m.GetGenericMethodDefinition().Name == methodName && 
                    m.GetGenericArguments().Length == genericArgumentsCount && 
                    CompareGenericArguments(m.GetGenericMethodDefinition().GetParameters(), parameters));

                if(UnderlyingMethod != null)
                {
                    UnderlyingMethod = UnderlyingMethod.MakeGenericMethod(genericArguments);
                }
            }
            else
            {
                var types = new Type[parametersCount];
                for(int i = 0; i < types.Length; i++)
                {
                    types[i] = reader.Types.Read().UnderlyingType;
                }

                UnderlyingMethod = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types, null);
            }
        }

        public MethodInfo UnderlyingMethod { get; private set; }

        private static bool CompareGenericArguments(ParameterInfo[] actual, TypeOrGenericTypeArgument[] expected)
        {
            if(actual.Length != expected.Length)
            {
                return false;
            }

            for(int i = 0; i < actual.Length; i++)
            {
                if(actual[i].ParameterType.IsGenericParameter)
                {
                    if(actual[i].ParameterType.GenericParameterPosition != expected[i].GenericTypeArgumentIndex)
                    {
                        return false;
                    }
                }
                else
                {
                    if(actual[i].ParameterType != expected[i].Type)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

