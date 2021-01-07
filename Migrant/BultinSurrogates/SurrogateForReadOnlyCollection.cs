//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal class SurrogateForReadOnlyCollection<T> : ISurrogateRestorer
    {
        public SurrogateForReadOnlyCollection(ReadOnlyCollection<T> readOnlyCollection)
        {
            content = new List<T>(readOnlyCollection);
        }

        public object Restore()
        {
            return new ReadOnlyCollection<T>(content);
        }

        private readonly List<T> content;
    }
}

