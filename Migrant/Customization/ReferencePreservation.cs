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

namespace Antmicro.Migrant.Customization
{
    /// <summary>
    /// Used with open stream serialization and tells serializer how to deal with references
    /// between different sessions of serialization.
    /// </summary>
    public enum ReferencePreservation
    {
        /// <summary>
        /// If the same object is written in a different serialization session, it is written as 
        /// a newly encountered object.
        /// </summary>
        DoNotPreserve,

        /// <summary>
        /// Identity of all objects is preserved between sessions. This option will, however, create
        /// a hard reference for each so far serialized object. This means that all objects serialized
        /// so for will live until open stream serializer is disposed.
        /// </summary>
        Preserve,

        /// <summary>
        /// Object identity is preserved without hard references. This option is conceptually the best one,
        /// can however vastly influence performance.
        /// </summary>
        UseWeakReference
    }
}

