// *******************************************************************
//
//  Copyright (c) 2012-2015, Antmicro Ltd <antmicro.com>
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
using System.Reflection;
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant.VersionTolerance
{
    internal class ModuleDescriptor
    {
        public static ModuleDescriptor CreateFromModule(Module module)
        {
            return new ModuleDescriptor(module);
        }

        public static ModuleDescriptor ReadFromStream(ObjectReader reader)
        {
            var module = new ModuleDescriptor();
            module.ReadModuleStamp(reader);
            return module;
        }

        public void ReadModuleStamp(ObjectReader reader)
        {
            ModuleAssembly = reader.ReadAssembly();
            GUID = reader.PrimitiveReader.ReadGuid();
            Name = reader.PrimitiveReader.ReadString();
        }

        public void WriteModuleStamp(ObjectWriter writer)
        {
            writer.TouchAndWriteAssemblyId(ModuleAssembly);
            writer.PrimitiveWriter.Write(GUID);
            writer.PrimitiveWriter.Write(Name);
        }

        public bool Equals(ModuleDescriptor obj, VersionToleranceLevel versionToleranceLevel)
        {
            if(versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowGuidChange))
            {
                return obj != null && obj.Name == Name && ModuleAssembly.Equals(obj.ModuleAssembly, versionToleranceLevel);
            }
            return obj != null && obj.GUID == GUID && ModuleAssembly.Equals(obj.ModuleAssembly, versionToleranceLevel);
        }
        
        private ModuleDescriptor()
        {
        }

        private ModuleDescriptor(Module module)
        {
            ModuleAssembly = AssemblyDescriptor.CreateFromAssembly(module.Assembly);
            Name = module.Name;
            GUID = module.ModuleVersionId;
        }

        public string Name { get; private set; }

        public Guid GUID { get; private set; } 

        public AssemblyDescriptor ModuleAssembly { get; private set; }
    }
}

