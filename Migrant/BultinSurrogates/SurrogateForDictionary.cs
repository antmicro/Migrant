//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.Collections.Generic;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal class SurrogateForDictionary<TKey, TVal> : ISurrogateRestorer
    {
        public SurrogateForDictionary(Dictionary<TKey, TVal> dic)
        {
            keys = new List<TKey>(dic.Keys.Count);
            values = new List<TVal>(dic.Keys.Count);

            foreach(var key in dic.Keys)
            {
                keys.Add(key);
                values.Add(dic[key]);
            }
        }

        public object Restore()
        {
            var result = new Dictionary<TKey, TVal>();
            for(var i = 0; i < keys.Count; i++)
            {
                result.Add(keys[i], values[i]);
            }

            return result;
        }

        private readonly List<TKey> keys;
        private readonly List<TVal> values;
    }
}

