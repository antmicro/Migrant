/*
  Copyright (c) 2013-2015 Antmicro <www.antmicro.com>

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
        /// Difference in guid is allowed which means that classes may be from different compilation of the same library.
        /// </summary>
        AllowGuidChange = 0x1,

        /// <summary>
        /// The new layout can have more fields that the old one. They are initialized to their default values.
        /// </summary>
        AllowFieldAddition = 0x2,

        /// <summary>
        /// The new layout can have less fields that the old one. Values of the missing one are ignored.
        /// </summary>
        AllowFieldRemoval = 0x4,

        /// <summary>
        /// Classes inheritance hirarchy can vary between new and old layout, e.g., base class can be removed.
        /// </summary>
        AllowInheritanceChainChange = 0x80,

        /// <summary>
        /// Assemblies version can very between new and old layout.
        /// </summary>
        AllowAssemblyVersionChange = 0x10
    }
}

