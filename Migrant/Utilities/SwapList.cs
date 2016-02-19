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
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.Utilities.SwapList"/> class.
        /// </summary>
        public SwapList()
        {
            items = new List<SwapListItem>();
            headIndex = -1;
        }

        /// <summary>
        /// Adds new key/value pair or replaces existing one if the key already exists.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        public void AddOrReplace(Type key, Delegate value)
        {
            InnerAddOrReplace(key, value, null);
        }

        /// <summary>
        /// Adds new key/dynamically generated pair or replaces one if the key already exists.
        /// </summary>
        /// <param name="genericType">Key.</param>
        /// <param name="actualDelegateGenerator">Function generating final value.</param>
        public void AddGenericTemplate(Type genericType, Func<Type, Delegate> actualDelegateGenerator)
        {
            InnerAddOrReplace(genericType, null, actualDelegateGenerator);
        }

        /// <summary>
        /// Gets a delegate by an index.
        /// </summary>
        /// <returns>Delegate at a given index.</returns>
        /// <param name="index">The index of the element.</param>
        public Delegate GetByIndex(int index)
        {
            return items[index].Value;
        }

        /// <summary>
        /// Finds the index of the delegate that matches given type.
        /// </summary>
        /// <returns>The index of delegate that matches given type or -1 if it could not be found.</returns>
        /// <param name="value">Type to match.</param>
        public int FindMatchingIndex(Type value)
        {
            var currentIndex = headIndex;
            while(currentIndex != -1)
            {
                var current = items[currentIndex];

                if(IsMatch(current.Key, value))
                {
                    if(current.Creator != null)
                    {
                        return InnerAddOrReplace(value, current.Creator(value), null);
                    }
                    return current.Value == null ? -1 : current.Id;
                }
                currentIndex = current.NextElementIndex;
            }
            return -1;
        }

        internal int Count { get { return items.Count; } }
        
        private int InnerAddOrReplace(Type key, Delegate value, Func<Type, Delegate> valueCreator)
        {
            var previousIndex = -1;
            var currentIndex = headIndex;
            while(currentIndex != -1)
            {
                var current = items[currentIndex];

                if(IsMatch(current.Key, key))
                {
                    if(current.Key == key)
                    {
                        items[currentIndex] = current.With(value: value);
                        return currentIndex;
                    }
                    break;
                }
                previousIndex = currentIndex;
                currentIndex = current.NextElementIndex;
            }

            var newItem = new SwapListItem(key, value, valueCreator, items.Count);
            if(previousIndex == -1)
            {
                newItem.NextElementIndex = headIndex;
                headIndex = newItem.Id;
            }
            else
            {
                newItem.NextElementIndex = items[previousIndex].NextElementIndex;
                items[previousIndex] = items[previousIndex].With(nextElementIndex: newItem.Id);
            }
            items.Add(newItem);
            return newItem.Id;
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

        private int headIndex;
        private readonly List<SwapListItem> items;

        private struct SwapListItem
        {
            public SwapListItem(Type key, Delegate value, Func<Type, Delegate> creator, int id) : this()
            {
                Key = key;
                Value = value;
                Id = id;
                NextElementIndex = -1;
                Creator = creator;
            }

            public SwapListItem With(int? nextElementIndex = null, Delegate value = null)
            {
                return new SwapListItem(Key, value ?? Value, Creator, Id) { NextElementIndex = nextElementIndex ?? this.NextElementIndex };
            }

            public Type Key { get; private set; }
            public Delegate Value { get; set; }
            public int NextElementIndex { get; set; }
            public int Id { get; private set; }
            public Func<Type, Delegate> Creator { get; private set; }
        }
    }
}

