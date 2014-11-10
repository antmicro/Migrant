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
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace Antmicro.Migrant
{
	/// <summary>
	/// Gives consecutive, unique identifiers for presented objects during its lifetime.
	/// Can also be used to retrive an object by its ID.
	/// </summary>
	/// <remarks>
	/// The first returned id is 0. For given object, if it was presented to the class
	/// earlier, the previously returned identificator is returned again. Note that the
	/// objects presented to class are remembered, so they will not be collected until
	/// the <c>ObjectIdentifier</c> lives.
	/// </remarks>
	public class ObjectIdentifier
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Antmicro.Migrant.ObjectIdentifier"/> class.
		/// </summary>
		public ObjectIdentifier()
		{
			generator = new ObjectIDGenerator();
			consecutiveIds = new Dictionary<long, int>();
			objects = new List<object>();
		}

		/// <summary>
		/// For a given object, returns its unique ID. The new ID is used if object was
		/// not presented to this class earlier, otherwise the previously returned is used.
		/// </summary>
		/// <returns>
		/// The object's unique ID.
		/// </returns>
		/// <param name='o'>
		/// An object to give unique ID for.
		/// </param>
		public int GetId(object o)
		{
			bool isNew;
			var id = generator.GetId(o, out isNew);
			if(isNew)
			{
				var localId = objects.Count;
				objects.Add(o);
				consecutiveIds.Add(id, localId);
				return localId;
			}
			return consecutiveIds[id];
		}

		/// <summary>
		/// For an ID which was previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object)" /> method,
		/// returns an object for which this ID was generated.
		/// </summary>
		/// <returns>
		/// The object for which given ID was returned.
		/// </returns>
		/// <param name='id'>
		/// The unique ID, previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object)" /> method.
		/// </param>
		public object GetObject(int id)
		{
			if(objects.Count <= id || id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}
			return objects[id];
		}

		/// <summary>
		/// For an ID which was previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object)" /> method,
		/// returns an object for which this ID was generated.
		/// </summary>
		/// <param name='id'>
		/// The unique ID, previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object)" /> method.
		/// </param>
		public object this[int id]
		{
			get
			{
				return GetObject(id);
			}
		}

		/// <summary>
		/// Gets the count of the unique objects presented to class. It is also
		/// the first unoccupied ID which will be returned for the new object.
		/// </summary>
		public int Count
		{
			get
			{
				return objects.Count;
			}
		}

		private readonly ObjectIDGenerator generator;
		private readonly Dictionary<long, int> consecutiveIds;
		private readonly List<object> objects;
	}
}

