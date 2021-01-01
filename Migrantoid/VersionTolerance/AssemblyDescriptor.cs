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
using System.Linq;
using Migrantoid.Customization;
using Migrantoid.Utilities;

namespace Migrantoid.VersionTolerance
{
    internal class AssemblyDescriptor : IIdentifiedElement
    {
        public AssemblyDescriptor()
        {
        }

        public AssemblyDescriptor(Assembly assembly)
        {
            UnderlyingAssembly = assembly;

            Name = assembly.GetName().Name;
            Version = assembly.GetName().Version;
            CultureName = assembly.GetName().CultureName;
            if (CultureName == string.Empty)
            {
                CultureName = "neutral";
            }
            Token = assembly.GetName().GetPublicKeyToken();
            CreateFullName();
        }

        public void Read(ObjectReader reader)
        {
            Name = reader.PrimitiveReader.ReadString();
            Version = reader.PrimitiveReader.ReadVersion();
            CultureName = reader.PrimitiveReader.ReadString();
            var tokenLength = reader.PrimitiveReader.ReadByte();
            switch(tokenLength)
            {
            case 0:
                Token = new byte[0];
                break;
            case 8:
                Token = reader.PrimitiveReader.ReadBytes(8);
                break;
            default:
                throw new ArgumentException("Wrong token length!");
            }

            CreateFullName();
            var assemblyName = new AssemblyName(FullName);
            UnderlyingAssembly = Assembly.Load(assemblyName);
        }

        public void Write(ObjectWriter writer)
        {
            writer.PrimitiveWriter.Write(Name);
            writer.PrimitiveWriter.Write(Version);
            writer.PrimitiveWriter.Write(CultureName);
            writer.PrimitiveWriter.Write((byte)Token.Length);
            writer.PrimitiveWriter.Write(Token);
        }

        public override bool Equals(object obj)
        {
            var objAsAssemblyDescriptor = obj as AssemblyDescriptor;
            if (objAsAssemblyDescriptor != null)
            {
                return FullName == objAsAssemblyDescriptor.FullName;
            }
            return obj != null && obj.Equals(this);
        }

        public bool Equals(AssemblyDescriptor obj, VersionToleranceLevel versionToleranceLevel)
        {
            if(versionToleranceLevel.HasFlag(VersionToleranceLevel.AllowAssemblyVersionChange))
            {
                return obj.Name == Name && obj.CultureName == CultureName && obj.Token.SequenceEqual(Token);
            }

            return Equals(obj);
        }

        public override int GetHashCode()
        {
            return FullName.GetHashCode();
        }

        public string FullName { get; private set; }

        public Assembly UnderlyingAssembly { get; private set; }
        public string Name { get; private set; }
        public Version Version { get; private set; }
        public string CultureName { get; private set; }
        public byte[] Token { get; private set; }

        private void CreateFullName()
        {
            FullName = string.Format("{0}, Version={1}, Culture={2}, PublicKeyToken={3}", Name, Version, CultureName, Token.Length == 0 ? "null" : String.Join(string.Empty, Token.Select(x => string.Format("{0:x2}", x)))); 
        }
    }

    internal static class PrimitiveWriterReaderExtensions
    {
        public static void Write(this PrimitiveWriter @this, Version version)
        {
            @this.Write(version.Major);
            @this.Write(version.Minor);
            @this.Write(version.Build);
            @this.Write(version.Revision);
        }
       
        public static Version ReadVersion(this PrimitiveReader @this)
        {
            var major = @this.ReadInt32();
            var minor = @this.ReadInt32();
            var build = @this.ReadInt32();
            var revision = @this.ReadInt32();

            if(revision != -1)
            {
                return new Version(major, minor, build, revision);
            }
            else if(build != -1)
            {
                return new Version(major, minor, build);
            }
            return new Version(major, minor);
        }
    }
}

