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
    public sealed class SwapList
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.Utilities.InheritanceAwareList{T}"/> class.
        /// </summary>
        public SwapList()
        {
            keys = new List<Type>();
            values = new List<Delegate>();
        }

        /// <summary>
        /// Adds new key/value pair or replaces existing one if the key already exists.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        public void AddOrReplace(Type key, Delegate value)
        {
            // we put generic surrogates at the end of the list, so they will match only
            // if there is no match with non-generic surrogates
            int i, lastPossibleIndex;
            if(Helpers.IsOpenGenericType(key))
            {
                lastPossibleIndex = keys.Count - 1;
                i = numberOfNonGenerics;
            }
            else
            {
                lastPossibleIndex = numberOfNonGenerics - 1;
                i = 0;
                numberOfNonGenerics++;
            }

            while(i <= lastPossibleIndex)
            {
                if(IsMatch(keys[i], key))
                {
                    if(keys[i] == key)
                    {
                        return;
                    }
                    break;
                }
                i++;
            }

            keys.Insert(i, key);
            values.Insert(i, value);
        }

        /// <summary>
        /// Gets a value by an index.
        /// </summary>
        /// <returns>Value at a given index.</returns>
        /// <param name="index">The index of the element.</param>
        public Delegate GetByIndex(int index)
        {
            return values[index];
        }

        public int FindMatchingIndex(Type value)
        {
            for(var i = 0; i < keys.Count; i++)
            {
                if(IsMatch(keys[i], value))
                {
                    return values[i] == null ? -1 : i;
                }
            }
            return -1;
        }

        private static bool IsMatch(Type candidate, Type value)
        { 
            if(Helpers.IsOpenGenericType(candidate) && value.IsGenericType)
            {
                return GenericIsMatch(candidate, value.GetGenericTypeDefinition());
            }
            return candidate.IsAssignableFrom(value);
        }

        private static bool GenericIsMatch(Type candidate, Type value)
        {
            if(candidate.IsInterface)
            {
                var interfaces = value.GetInterfaces();
                if(Array.IndexOf(interfaces, candidate) != -1)
                {
                    return true;
                }
            }
            while(value != null && value != typeof(object))
            {
                if(value == candidate)
                {
                    return true;
                }
                value = value.BaseType;
                if(!value.IsGenericType)
                {
                    break;
                }
                value = value.GetGenericTypeDefinition();
            }
            return false;
        }

        private int numberOfNonGenerics;
        private readonly List<Type> keys;
        private readonly List<Delegate> values;

    }
}

