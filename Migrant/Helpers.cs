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
using ImpromptuInterface;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;

namespace AntMicro.Migrant
{
    internal static class Helpers
    {
        internal static bool TryGetCollectionCountAndElementType(object o, out int count, out Type formalElementType)
        {
			bool fake, fake2, fake3;
            if(IsCollection(o.GetType(), out formalElementType, out fake, out fake2, out fake3))
            {
                count = (int)Impromptu.InvokeGet(o, "Count");
                return true;
            }
            count = -1;
            return false;
        }

		internal static bool CheckTransientNoCache(Type type)
		{
			return type.IsDefined(typeof(TransientAttribute), true);
		}

        public static bool TryGetDictionaryCountAndElementTypes(object o, out int count, out Type formalKeyType, out Type formalValueType)
        {
            if(IsDictionary(o.GetType(), out formalKeyType, out formalValueType))
            {
                count = (int)Impromptu.InvokeGet(o, "Count");
                return true;
            }
            count = -1;
            return false;
        }

		// TODO: refactor with enum as a result instead of isGeneric etc
		// and join with IsDictionary
		public static bool IsCollection(Type actualType, out Type formalElementType, out bool isGeneric, out bool isGenericallyIterable, out bool isDictionary)
        {
            formalElementType = typeof(object);
            var ifaces = actualType.GetInterfaces();
            var result = false;
			isGeneric = false;
			isGenericallyIterable = false;
			isDictionary = false;
			var isGenericDictionary = false;
            foreach(var iface in ifaces)
            {
                if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    formalElementType = iface.GetGenericArguments()[0];
					isGeneric = true;
					isGenericallyIterable = true;
                    result = true;
                }
				if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
				{
					isGenericDictionary = true;
				}
                if(iface == typeof(ICollection))
                {
                    result = true;
                }
				if(iface == typeof(IDictionary))
				{
					isDictionary = true;
				}
                if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    formalElementType = iface.GetGenericArguments()[0];
					isGenericallyIterable = true;
                }
            }
			if(isGenericDictionary)
			{
				// we favour treating as a generic dictionary if the collection implements both
				isDictionary = false;
			}
            return result;
        }

        public static bool IsDictionary(Type actualType, out Type formalKeyType, out Type formalValueType)
        {
            formalKeyType = typeof(object);
            formalValueType = typeof(object);
            var ifaces = actualType.GetInterfaces();
            var result = false;
            foreach(var iface in ifaces)
            {
                if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    var arguments = iface.GetGenericArguments();
                    formalKeyType = arguments[0];
                    formalValueType = arguments[1];
                    return true;
                }
                if(iface == typeof(IDictionary))
                {
                    result = true;
                }
            }
            return result;
        }

        public static bool CanBeCreatedWithDataOnly(Type actualType)
        {
            return actualType == typeof(string) || actualType.IsValueType || actualType.IsArray || typeof(MulticastDelegate).IsAssignableFrom(actualType);
        }

		private static int GetBoundary(long currentPosition)
		{
		    var index = -1;
            while(currentPosition > PaddingBoundaries[++index]);
		    
            var boundary = PaddingBoundaries[index];
            if(boundary == int.MaxValue)
            {
                boundary = PaddingBoundaries[index - 1];
            }
			return boundary;
		}

        public static int GetCurrentPaddingValue(long currentPosition)
        {
			var boundary = GetBoundary(currentPosition);
            if(currentPosition % boundary == 0)
            {
                return 0;
            }
            return boundary - (int)(currentPosition & (boundary - 1));
        }

        public static int GetNextBytesToRead(long currentPosition)
        {
			var boundary = GetBoundary(currentPosition);
            var result = boundary - (int)(currentPosition & (boundary - 1));
            return result;
        }

        public static void ReadOrThrow(this Stream @this, byte[] buffer, int offset, int count)
        {
            while(count > 0)
            {
                var readThisTime = @this.Read(buffer, offset, count);
                if(readThisTime == 0)
                {
                    throw new EndOfStreamException(
                        string.Format("End of stream reached while {0} bytes more expected.", count));
                }
                count -= readThisTime;
                offset += readThisTime;
            }
        }

        public static object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

		public static IEnumerable<MethodInfo> GetMethodsWithAttribute(Type attributeType, Type objectType)
		{
			var derivedMethods = objectType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(x => x.IsDefined(attributeType, false));
			var baseType = objectType.BaseType;
			return baseType == null ? derivedMethods : derivedMethods.Union(GetMethodsWithAttribute(attributeType, baseType));
		}

        public static void InvokeAttribute(Type attributeType, object o)
        {
			var methodsToInvoke = GetMethodsWithAttribute(attributeType, o.GetType());
            foreach(var method in methodsToInvoke)
            {
                method.Invoke(o, new object[0]);
            }
        }

		public static Action GetDelegateWithAttribute(Type attributeType, object o)
		{
			var methodsToInvoke = GetMethodsWithAttribute(attributeType, o.GetType()).ToList();
			if(methodsToInvoke.Count == 0)
			{
				return null;
			}
			return methodsToInvoke.Select(x => (Action)Delegate.CreateDelegate(typeof(Action), o, x)).Aggregate((x, y) => (Action)Delegate.Combine(x, y));
		}

        public static bool IsNotTransient(this FieldInfo fieldInfo)
        {
            return !fieldInfo.Attributes.HasFlag(FieldAttributes.Literal) && !fieldInfo.IsDefined(typeof(TransientAttribute), false);
        }

        public static int MaximalPadding
        {
            get
            {
                return PaddingBoundaries[PaddingBoundaries.Length - 2];
            }
        }

		public static IEnumerable<FieldInfo> GetAllFields(this Type t, bool recursive = true)
        {            
            if(t == null)
            {
                return Enumerable.Empty<FieldInfo>();
            }
            if(recursive)
            {
                return t.GetFields(DefaultBindingFlags).Union(GetAllFields(t.BaseType));
            }
            return t.GetFields(DefaultBindingFlags);
        }

		public static MethodInfo GetMethodInfo(Expression<Action> expression)
		{
			var methodCall = (MethodCallExpression)expression.Body;
			return methodCall.Method;
		}

		public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
		{
			var methodCall = (MethodCallExpression)expression.Body;
			return methodCall.Method;
		}

		public static MethodInfo GetMethodInfo<T, TParam1>(Expression<Action<T, TParam1>> expression)
		{
			var methodCall = (MethodCallExpression)expression.Body;
			return methodCall.Method;
		}

		internal static SerializationType GetSerializationType(Type type)
		{
			if(Helpers.CheckTransientNoCache(type))
            {
                return SerializationType.Transient;
            }
			if(type.IsValueType)
            {
				return SerializationType.Value;
            }
			return SerializationType.Reference;
		}

        public static readonly DateTime DateTimeEpoch = new DateTime(2000, 1, 1);

        private static readonly int[] PaddingBoundaries = new [] { 128, 1024, 4096, int.MaxValue };

		private const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | 
                BindingFlags.Instance | BindingFlags.DeclaredOnly;
    }
}

