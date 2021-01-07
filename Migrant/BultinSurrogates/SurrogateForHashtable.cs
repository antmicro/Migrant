//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System.Collections;
using System.Collections.Generic;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal class SurrogateForHashtable
    {
        public SurrogateForHashtable(Hashtable hashtable)
        {
            keys = new List<object>();
            values = new List<object>();

            foreach(var key in hashtable.Keys)
            {
                keys.Add(key);
                values.Add(hashtable[key]);
            }
        }

        public object Restore()
        {
            var result = new Hashtable();
            for(var i = 0; i < keys.Count; i++)
            {
                result.Add(keys[i], values[i]);
            }

            return result;
        }

        private readonly List<object> keys;
        private readonly List<object> values;
    }
}

