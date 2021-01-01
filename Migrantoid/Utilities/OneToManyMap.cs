// *******************************************************************
//
//  Copyright (c) 2012-2016, Antmicro Ltd <antmicro.com>
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

namespace Migrantoid.Utilities
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

