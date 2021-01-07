//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;

namespace Antmicro.Migrant
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

