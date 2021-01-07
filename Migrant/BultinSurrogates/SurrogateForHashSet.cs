//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal class SurrogateForHashSet<T> : ISurrogateRestorer
    {
        public SurrogateForHashSet(HashSet<T> set)
        {
            content = new List<T>(set);
        }

        public object Restore()
        {
            return new HashSet<T>(content);
        }

        private readonly List<T> content;
    }
}

