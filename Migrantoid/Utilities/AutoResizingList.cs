/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Collections;

namespace Migrantoid.Utilities
{
    internal sealed class AutoResizingList<T> : IList<T>
    {
        public AutoResizingList(int initialCapacity = 4)
        {
            this.initialCapacity = initialCapacity;
            Clear();
        }

        public int Count { get; private set; }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public T this[int index]
        {
            get
            {
                ResizeTo(index + 1);
                return data[index];
            }
            set
            {
                ResizeTo(index + 1);
                data[index] = value;
            }
        }

        public void Add(T item)
        {
            ResizeTo(Count + 1);
            data[Count - 1] = item;
        }

        public void Clear()
        {
            data = new T[initialCapacity];
            Count = 0;
        }

        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo(T[] array, int index)
        {
            Array.Copy(data, array, Count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            var count = Count;
            for(var i = 0; i < count; i++)
            {
                yield return data[i];
            }
        }

        public int IndexOf(T item)
        {
            var index = Array.IndexOf(data, item);
            if(index >= Count)
            {
                return -1;
            }
            return index;
        }

        public void Insert(int index, T item)
        {
            if(index >= Count)
            {
                this[index] = item;
                return;
            }
            ResizeTo(Count + 1);
            for(var i = Count - 2; i >= index; i--)
            {
                data[i + 1] = data[i];
            }
            data[index] = item;
        }

        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if(index == -1)
            {
                return false;
            }
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            ResizeTo(Count - 1);
            var count = Count;
            for(var i = index; i < count; i++)
            {
                data[i] = data[i + 1];
            }
        }


        // Method needed for accessing MethodInfo of setter of an element
        public void SetItem(int index, T value)
        {
            ResizeTo(index + 1);
            data[index] = value;
        }

        private void ResizeTo(int neededSize)
        {
            if(neededSize < 0)
            {
                throw new ArgumentException("Index cannot be negative.");
            }
            Count = Math.Max(Count, neededSize);
            if(data.Length >= neededSize)
            {
                return;
            }
            var newData = new T[Math.Max(data.Length * 2, neededSize)];
            data.CopyTo(newData, 0);
            data = newData;
        }

        private T[] data;
        private readonly int initialCapacity;
    }
}

