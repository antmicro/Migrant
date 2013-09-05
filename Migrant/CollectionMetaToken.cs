/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

  Authors:
   * Mateusz Holenko (mholenko@antmicro.com)

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

namespace AntMicro.Migrant
{
	public class CollectionMetaToken
	{
		public bool IsCollection { get { return IsLinearCollection || IsDictionary; } }

		public bool IsGeneric { get { return FormalElementType != null || FormalKeyType != null; } }

		public bool IsGenericallyIterable { get; private set; }

		private bool _isLinearCollection;
		public bool IsLinearCollection { get { return _isLinearCollection || FormalElementType != null; } }

		private bool _isDictionaryCollection;
		public bool IsDictionary { get { return _isDictionaryCollection || FormalKeyType != null; } }

		public Type FormalElementType { get; private set; }

		public Type FormalKeyType { get; private set; }

		public Type FormalValueType { get; private set; }

		public void SetLinearGenericCollection(Type formalElementType)
		{
			FormalElementType = formalElementType;
			IsGenericallyIterable = true;
		}

		public void SetLinearCollection()
		{
			_isLinearCollection = true;
		}

		public void SetDictionaryGenericCollection(Type formalKeyType, Type formalValueType)
		{
			FormalKeyType = formalKeyType;
			FormalValueType = formalValueType;
		}

		public void SetDictionaryCollection()
		{
			_isDictionaryCollection = true;
		}
	}
}

