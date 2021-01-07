//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Concurrent;

namespace Antmicro.Migrant.Utilities
{
    internal static class TypeProvider
    {
        public static Type GetType(string name)
        {
            return cache.GetOrAdd(name, x => Type.GetType(x));
        }

        private readonly static ConcurrentDictionary<string, Type> cache = new ConcurrentDictionary<string, Type>();
    }
}

