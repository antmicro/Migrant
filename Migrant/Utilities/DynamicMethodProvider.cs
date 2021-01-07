//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;

namespace Antmicro.Migrant.Utilities
{
    internal class DynamicMethodProvider<T> where T : class
    {
        public DynamicMethodProvider(Func<Type, T> creator)
        {
            cache = new Dictionary<Type, T>();
            dynamicMethodCreator = creator;
        }

        public T GetOrCreate(Type t)
        {
            T method;
            if(!cache.TryGetValue(t, out method))
            {
                method = dynamicMethodCreator(t);
                cache.Add(t, method);
            }
            return method;
        }

        private readonly Dictionary<Type, T> cache;
        private readonly Func<Type, T> dynamicMethodCreator;
    }
}

