//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;

namespace Antmicro.Migrant.Utilities
{
    internal class OneToManyMap<TKey, TVal>
    {
        public OneToManyMap()
        {
            keyToValue = new Dictionary<TKey, TVal>();
            valueToKeys = new Dictionary<TVal, List<TKey>>();
        }

        public OneToManyMap(OneToManyMap<TKey, TVal> copy) : this()
        {
            foreach(var c in copy.Keys)
            {
                Add(c, copy[c]);
            }
        }

        public void Add(TKey key, TVal value)
        {
            keyToValue.Add(key, value);
            List<TKey> keys;
            if(!valueToKeys.TryGetValue(value, out keys))
            {
                keys = new List<TKey>();
                valueToKeys.Add(value, keys);
            }
            keys.Add(key);
        }

        public void Remove(TKey key)
        {
            valueToKeys[keyToValue[key]].Remove(key);
            keyToValue.Remove(key);
        }

        public int Count
        {
            get { return keyToValue.Count; }
        }

        public TVal this[TKey key]
        {
            get
            {
                return keyToValue[key];
            }
        }

        public IEnumerable<TKey> this[TVal val]
        {
            get
            {
                return valueToKeys[val];
            }
        }

        public IEnumerable<TKey> Keys
        {
            get
            {
                return keyToValue.Keys;
            }
        }

        public IEnumerable<TVal> Values
        {
            get
            {
                return valueToKeys.Keys;
            }
        }

        private Dictionary<TKey, TVal> keyToValue;
        private Dictionary<TVal, List<TKey>> valueToKeys;

        public bool TryGetValue(TKey key, out TVal value)
        {
            return keyToValue.TryGetValue(key, out value);
        }

        public bool TryGetKeysWithValue(TVal value, out IEnumerable<TKey> keys)
        {
            bool result;
            List<TKey> localKeys;
            result = valueToKeys.TryGetValue(value, out localKeys);
            keys = localKeys;
            return result;
        }
    }
}

