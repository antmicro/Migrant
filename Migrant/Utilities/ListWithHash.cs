using System.Collections.Generic;
using System.Collections;

namespace AntMicro.Migrant.Utilities
{
	// TODO: implement and use IList, IDictionary
	internal class ListWithHash<T> : IEnumerable<T>
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
	}
}

