/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Mateusz Holenko (mholenko@antmicro.com)

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;

namespace Antmicro.Migrant.Customization
{
    /// <summary>
    /// Level of the version tolerance, that is how much layout of the deserialized type can differ
    /// from the (original) layout of the serialized type.
    /// </summary>
    [Flags]
    public enum VersionToleranceLevel
    {
        /// <summary>
        /// Both new and old layout have to have the same base classes, number of fields, with the same names and types.
        /// </summary>
        ExactLayout = 0,

        /// <summary>
        /// Both serialized and serialized classes must have identical module ID (which is GUID). In other words
        /// that means these assemblies are from one and the same compilation.
        /// </summary>
        Guid = 0x1,

        /// <summary>
        /// The new layout can have more fields that the old one. They are initialized to their default values.
        /// </summary>
        FieldAddition = 0x2,

        /// <summary>
        /// The new layout can have less fields that the old one. Values of the missing one are ignored.
        /// </summary>
        FieldRemoval = 0x4,

        /// <summary>
        /// In the new layout fields can be moved to another classes in the inheritance hierarchy. 
        /// Fields will be matched by names, therefore this flag cannot be used in classes with multiple fields with the same name.
        /// </summary>
        FieldMove = 0x8,

        /// <summary>
        /// Classes inheritance hirarchy can vary between new and old layout, e.g., base class can be removed.
        /// </summary>
        InheritanceChainChange = 0x10,

        /// <summary>
        /// Classes names can vary between new and old layout.
        /// Fields will be matched by names, therefore this flag cannot be used in classes with multiple fields with the same name.
        /// </summary>
        TypeNameChanged = 0x20,

        /// <summary>
        /// Assemblies version can very between new and old layout.
        /// </summary>
        AssemblyVersionChanged = 0x40
    }
}

