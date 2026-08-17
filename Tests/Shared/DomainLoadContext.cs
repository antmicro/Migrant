//
// Copyright (c) 2012-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.Runtime.Loader;
using System.Reflection;
using System.IO;

namespace Antmicro.Migrant.Tests.Shared
{
    public class DomainLoadContext : AssemblyLoadContext
    {
        public DomainLoadContext(string name, string domainPath) : base(name, isCollectible: true)
        {
            this.domainPath = domainPath;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Prioritize assemblies located in domainPath over 'default' ones.
            var assemblyPath = Path.Combine(domainPath, $"{assemblyName.Name}.dll");
            if (File.Exists(assemblyPath))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // Missing assemblies will be searched for by a 'standard' runtime load.
            return null;
        }

        private readonly string domainPath;
    }
}
