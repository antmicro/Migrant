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
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Collections;

namespace AntMicro.Migrant
{
	public class CollectionMetaToken
	{
        public bool IsCollection { get; private set; }

        public bool IsDictionary { get; private set; }

        public bool IsGeneric { get; private set; }

		public bool IsGenericallyIterable { get; private set; }

		public Type FormalElementType { get; private set; }

		public Type FormalKeyType { get; private set; }

		public Type FormalValueType { get; private set; }

        public MethodInfo CountMethod { get; set; }

        public Type ActualType { get; private set; }

        public CollectionMetaToken(Type actualType) 
        {
            ActualType = actualType;
            FormalElementType = typeof(object);
            FormalKeyType = typeof(object);
            FormalValueType = typeof(object);

            var ifaces = actualType.GetInterfaces();
            foreach(var iface in ifaces)
            {
                if(iface.IsGenericType)
                {
                    if(iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                    {
                        IsCollection = true;
                        IsGeneric = true;
                        IsGenericallyIterable = true;

                        FormalElementType = iface.GetGenericArguments()[0];

                        CountMethod = iface.GetProperty("Count").GetGetMethod();
                    }
                    else if (iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        IsGenericallyIterable = true;

                        FormalElementType = iface.GetGenericArguments()[0];

                        CountMethod = typeof(Enumerable).GetMethods().Single(m => m.GetParameters().Length == 1 && m.Name == "Count").MakeGenericMethod(iface.GetGenericArguments()[0]);
                    }
                    else if(iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    {
                        IsCollection = true;
                        IsDictionary = true;
                        IsGeneric = true;

                        var arguments = iface.GetGenericArguments();
                        FormalKeyType = arguments[0];
                        FormalValueType = arguments[1];

                        CountMethod = typeof(ICollection<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(arguments[0], arguments[1])).GetProperty("Count").GetGetMethod();
                    }
                }
                else if(iface == typeof(ICollection))
                {
                    IsCollection = true;

                    CountMethod = typeof(ICollection).GetProperty("Count").GetGetMethod();
                }
                else if(iface == typeof(IDictionary))
                {
                    IsCollection = true;
                    IsDictionary = true;

                    CountMethod = typeof(ICollection).GetProperty("Count").GetGetMethod();
                }
            }
        }
	}
}

