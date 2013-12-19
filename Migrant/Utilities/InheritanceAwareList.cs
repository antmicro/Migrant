// *******************************************************************
//
//  Copyright (c) 2012-2013, Antmicro Ltd
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
namespace Antmicro.Migrant.Utilities
{
    /// <summary>
    /// Key/value pair list in which more generic types are always inserted after their
    /// specializations.
    /// </summary>
    public sealed class InheritanceAwareList<T> : IEnumerable<KeyValuePair<Type, T>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.Utilities.InheritanceAwareList{T}"/> class.
        /// </summary>
        public InheritanceAwareList()
        {
            keys = new List<Type>();
            values = new List<T>();
        }

        /// <summary>
        /// Adds new key/value pair or replaces existing one if the key already exists.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        public void AddOrReplace(Type key, T value)
        {
            var i = 0;
            for(; i < keys.Count; i++)
            {
                if(keys[i].IsAssignableFrom(key))
                {
                    if(keys[i] == key)
                    {
                        return;
                    }
                    break;
                }
            }
            keys.Insert(i, key);
            values.Insert(i, value);
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An object that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<Type, T>> GetEnumerator()
        {
            for(var i = 0; i < keys.Count; i++)
            {
                yield return new KeyValuePair<Type, T>(keys[i], values[i]);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private readonly List<Type> keys;
        private readonly List<T> values;

    }
}

