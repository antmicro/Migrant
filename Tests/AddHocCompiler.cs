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
using System;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Antmicro.Migrant;

namespace Antmicro.Migrant.Tests
{
    public class AdHocCompiler
    {
        public static void Compile(string sourcePath, string outputPath, IEnumerable<string> referencedLibraries = null)
        {
            using(var provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } }))
            {
                var parameters = new CompilerParameters { GenerateInMemory = false, GenerateExecutable = false, OutputAssembly = outputPath };
                parameters.ReferencedAssemblies.Add("mscorlib.dll");
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                //parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(Serializer)).Location); // Migrant
                if(referencedLibraries != null)
                {
                    foreach(var lib in referencedLibraries)
                    {
                        parameters.ReferencedAssemblies.Add(lib);
                    }
                }

                var result = provider.CompileAssemblyFromFile(parameters, new[] { sourcePath });
                if(result.Errors.HasErrors)
                {
                    var errors = result.Errors.Cast<object>().Aggregate(string.Empty, (current, error) => current + ("\n" + error));
                    throw new Exception(string.Format("There were compilation errors:\n{0}", errors));
                }
            }
        }

        public static void CompileSource(string source, string outputPath, IEnumerable<string> referencedLibraries = null)
        {
            using(var provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } }))
            {
                var parameters = new CompilerParameters { GenerateInMemory = false, GenerateExecutable = false, OutputAssembly = outputPath };
                parameters.ReferencedAssemblies.Add("mscorlib.dll");
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                if(referencedLibraries != null)
                {
                    foreach(var lib in referencedLibraries)
                    {
                        parameters.ReferencedAssemblies.Add(lib);
                    }
                }

                var result = provider.CompileAssemblyFromSource(parameters, new[] { source });
                if(result.Errors.HasErrors)
                {
                    var errors = result.Errors.Cast<object>().Aggregate(string.Empty, (current, error) => current + ("\n" + error));
                    throw new Exception(string.Format("There were compilation errors:\n{0}", errors));
                }
            }
        }
    }
}