/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)

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

using System.Collections.Generic;
using System.Collections;
using System;

namespace Antmicro.Migrant.Utilities
{
    internal class ListWithHash<T> : IEnumerable<T>, IList<T>, ISet<T>
	{
		public ListWithHash()
		{
			list = new List<T>();
			set = new HashSet<T>();
		}

		public bool Add(T element)
		{
			if(!set.Add(element))
			{
				return false;
			}
			list.Add(element);
			return true;
		}

		public int Count
		{
			get
			{
				return list.Count;
			}
		}

		public T this[int index]
		{
			get
			{
				return list[index];
			}

            set
            {
                throw new NotSupportedException();
            }
		}

		public IEnumerator<T> GetEnumerator()
		{
			return list.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)list).GetEnumerator();
		}

		private readonly List<T> list;
		private readonly HashSet<T> set;

        #region IList implementation

        public int IndexOf(T item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            if(!set.Add(item))
            {
                return;
            }
            list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            var item = list[index];
            list.RemoveAt(index);
            set.Remove(item);
        }

        #endregion

        #region ICollection implementation

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public void Clear()
        {
            list.Clear();
            set.Clear();
        }

        public bool Contains(T item)
        {
            return set.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            list.Remove(item);
            return set.Remove(item);
        }

        public bool IsReadOnly { get { return true; } }

        #endregion

        #region ISet implementation

        public void ExceptWith(IEnumerable<T> other)
        {
            set.ExceptWith(other);
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            set.IntersectWith(other);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return set.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return set.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return set.SetEquals(other);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            set.SymmetricExceptWith(other);
        }

        public void UnionWith(IEnumerable<T> other)
        {
            set.UnionWith(other);
        }

        #endregion
	}
}

