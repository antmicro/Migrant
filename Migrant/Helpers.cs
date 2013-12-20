/*
  Copyright (c) 2012-2013 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using Antmicro.Migrant.Utilities;

namespace Antmicro.Migrant
{
	internal static class Helpers
	{
		internal static bool CheckTransientNoCache(Type type)
		{
			return type.IsDefined(typeof(TransientAttribute), true);
		}

        public static bool CanBeCreatedWithDataOnly(Type actualType, bool treatCollectionAsUserObject = false)
		{
			return actualType == typeof(string) || actualType.IsValueType || actualType.IsArray || typeof(MulticastDelegate).IsAssignableFrom(actualType)
                || (!treatCollectionAsUserObject && (actualType.IsGenericType && typeof(ReadOnlyCollection<>).IsAssignableFrom(actualType.GetGenericTypeDefinition())));
		}

		private static int GetBoundary(long currentPosition)
		{
			foreach(var padding in PaddingBoundaries)
			{
				if(currentPosition <= padding)
				{
					return padding;
				}
			}
			return MaximalPadding;
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
            return baseType == null ? derivedMethods : GetMethodsWithAttribute(attributeType, baseType).Union(derivedMethods);
		}

		public static void InvokeAttribute(Type attributeType, object o)
		{
			var methodsToInvoke = GetMethodsWithAttribute(attributeType, o.GetType());
			foreach(var method in methodsToInvoke)
			{
				var methodToInvoke = (Action)Delegate.CreateDelegate(typeof(Action), method.IsStatic ? null : o, method);
				methodToInvoke();
			}
		}

		public static Action GetDelegateWithAttribute(Type attributeType, object o)
		{
			var methodsToInvoke = GetMethodsWithAttribute(attributeType, o.GetType()).ToList();
			if(methodsToInvoke.Count == 0)
			{
				return null;
			}
			return methodsToInvoke.Select(x => (Action)Delegate.CreateDelegate(typeof(Action), x.IsStatic ? null : o, x)).Aggregate((x, y) => (Action)Delegate.Combine(x, y));
		}

		public static bool IsNotTransient(this FieldInfo fieldInfo)
		{
			return (fieldInfo.Attributes & FieldAttributes.Literal) == 0 && !fieldInfo.IsDefined(typeof(TransientAttribute), false);
		}

		public static int MaximalPadding
		{
			get
			{
				return PaddingBoundaries[PaddingBoundaries.Length - 1];
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

		public static FieldInfo GetFieldInfo<T, TResult>(Expression<Func<T, TResult>> expression)
		{
			var mexpr = expression.Body as MemberExpression;
			if(mexpr == null)
			{
				return null;
			}

			return mexpr.Member as FieldInfo;
		}

		public static MethodInfo GetPropertyGetterInfo<T, TResult>(Expression<Func<T, TResult>> expression)
		{
			var mexpr = expression.Body as MemberExpression;
			if(mexpr == null)
			{
				return null;
			}

			var pinfo = mexpr.Member as PropertyInfo;
			if(pinfo == null)
			{
				return null;
			}

			return pinfo.GetGetMethod();
		}

		public static MethodInfo GetMethodInfo(Expression<Action> expression)
		{
			var methodCall = (MethodCallExpression)expression.Body;
			return methodCall.Method;
		}

		public static MethodInfo GetMethodInfo<T, TParam1>(Expression<Func<T, TParam1>> expression)
		{
			var methodCall = expression.Body as MethodCallExpression;
			if(methodCall == null)
			{
				// perhaps we have here UnaryExpression wrapping the MethodCallExpression
				methodCall = (expression.Body as UnaryExpression).Operand as MethodCallExpression;
			}
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

        internal static void SwapObjectWithSurrogate(ref object o, InheritanceAwareList<Delegate> swapList)
		{
			var type = o.GetType();
			foreach(var swapCandidate in swapList)
			{
				if(swapCandidate.Key.IsAssignableFrom(type))
				{
					o = swapCandidate.Value.DynamicInvoke(new object[] { o });
					break;
				}
			}
		}

		public static bool IsWriteableByPrimitiveWriter(Type type)
		{
			return TypesWriteableByPrimitiveWriter.Contains(type);
		}

		static Helpers()
		{
			TypesWriteableByPrimitiveWriter = typeof(PrimitiveWriter).GetMethods().Where(x => x.Name == "Write").Select(x => x.GetParameters()[0].ParameterType).ToArray();
		}

		public static readonly DateTime DateTimeEpoch = new DateTime(2000, 1, 1);

		private static readonly Type[] TypesWriteableByPrimitiveWriter;
		private static readonly int[] PaddingBoundaries = {
				128,
				1024,
				4096
			};

		private const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | 
			BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public const byte TickIndicator = 255;
	}
}

