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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Linq;
using System.Collections;

namespace Antmicro.Migrant
{
    internal class CollectionMetaToken
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
            if(!SpeciallySerializedCollections.Any(type => (actualType.IsGenericType && type == actualType.GetGenericTypeDefinition()) || type == actualType))
            {
                IsCollection = false;
                return;
            }

            ActualType = actualType;
            FormalElementType = typeof(object);
            FormalKeyType = typeof(object);
            FormalValueType = typeof(object);

            var ifaces = actualType.GetInterfaces();
            foreach(var prior in CollectionPriorities)
            {
                // there is really no access to modified closure as NRefactory suggests
                var iface = ifaces.FirstOrDefault(x => (x.IsGenericType ? x.GetGenericTypeDefinition() : x) == prior.Item1);
                if(iface != null)
                {
                    prior.Item2(iface, this);
                    return;
                }
            }
        }

        private static readonly Type[] SpeciallySerializedCollections = {
            typeof(List<>),
            typeof(ReadOnlyCollection<>),
            typeof(Dictionary<,>),
            typeof(HashSet<>),
            typeof(Queue<>),
            typeof(Stack<>),
            typeof(BlockingCollection<>),
            typeof(Hashtable)
        };

        private static readonly Tuple<Type, Action<Type, CollectionMetaToken>>[] CollectionPriorities = 
        {
            Tuple.Create<Type, Action<Type, CollectionMetaToken>>(typeof(IDictionary<,>), 
                (iface, cmt) => {
                    cmt.IsCollection = true;
                    cmt.IsDictionary = true;
                    cmt.IsGeneric = true;
                    var arguments = iface.GetGenericArguments();
                    cmt.FormalKeyType = arguments[0];
                    cmt.FormalValueType = arguments[1];
                    cmt.CountMethod = typeof(ICollection<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(arguments[0], arguments[1])).GetProperty("Count").GetGetMethod();
                }),
            Tuple.Create<Type, Action<Type, CollectionMetaToken>>(typeof(ICollection<>),
                (iface, cmt) => {
                    cmt.IsCollection = true;
                    cmt.IsGeneric = true;
                    cmt.IsGenericallyIterable = true;
                    cmt.FormalElementType = iface.GetGenericArguments()[0];
                    cmt.CountMethod = iface.GetProperty("Count").GetGetMethod();
                }),
            Tuple.Create<Type, Action<Type, CollectionMetaToken>>(typeof(IEnumerable<>),
                (iface, cmt) => {
                    cmt.IsGenericallyIterable = true;
                    cmt.FormalElementType = iface.GetGenericArguments()[0];
                    cmt.CountMethod = typeof(Enumerable).GetMethods().Single(m => m.GetParameters().Length == 1 && m.Name == "Count").MakeGenericMethod(iface.GetGenericArguments()[0]);
                }),
            Tuple.Create<Type, Action<Type, CollectionMetaToken>>(typeof(IDictionary),
                (iface, cmt) => {
                    cmt.IsCollection = true;
                    cmt.IsDictionary = true;
                    cmt.CountMethod = typeof(ICollection).GetProperty("Count").GetGetMethod();
                }),
            Tuple.Create<Type, Action<Type, CollectionMetaToken>>(typeof(ICollection),                 
                (iface, cmt) => {
                    cmt.IsCollection = true;
                    cmt.CountMethod = typeof(ICollection).GetProperty("Count").GetGetMethod();
                })
        };
	}
}

