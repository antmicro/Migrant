// *******************************************************************
//
//  Copyright (c) 2011-2014, Antmicro Ltd <antmicro.com>
//
//  Authors:
//   * Konrad Kruczynski (kkruczynski@antmicro.com)
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
using System.Collections.Generic;

namespace Migrantoid
{
    /// <summary>
    /// A context of object identifier, i.e. all identified objects (weakly referenced) with their identifiers. 
    /// </summary>
    public sealed class ObjectIdentifierContext
    {
        internal ObjectIdentifierContext(List<object> objects)
        {
            references = new WeakReference[objects.Count];
            for(var i = 0; i < references.Length; i++)
            {
                references[i] = new WeakReference(objects[i]);
            }
        }

        internal List<object> GetObjects()
        {
            var result = new List<object>(references.Length);
            for(var i = 0; i < references.Length; i++)
            {
                result.Add(references[i].Target);
            }
            return result;
        }

        private readonly WeakReference[] references;
    }
}

