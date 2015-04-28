/*
  Copyright (c) 2012-2015 Antmicro <www.antmicro.com>

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
        public static bool CanBeCreatedWithDataOnly(Type actualType, bool treatCollectionAsUserObject = false)
        {
            return actualType == typeof(string) || actualType.IsValueType || actualType.IsArray || typeof(MulticastDelegate).IsAssignableFrom(actualType)
            || (!treatCollectionAsUserObject && (actualType.IsGenericType && typeof(ReadOnlyCollection<>).IsAssignableFrom(actualType.GetGenericTypeDefinition())));
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

        public static byte ReadByteOrThrow(this Stream @this)
        {
            var result = @this.ReadByte();
            if(result < 0)
            {
                throw new EndOfStreamException("End of stream reached.");
            }
            return (byte)result;
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

        public static bool IsTransient(this FieldInfo finfo)
        {
            return finfo.IsDefined(typeof(TransientAttribute), false);
        }

        public static bool IsConstructor(this FieldInfo finfo)
        {
            return finfo.IsDefined(typeof(ConstructorAttribute), false);
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

        public static IEnumerable<Tuple<Type, IEnumerable<FieldInfo>>> GetAllFieldsStructurized(this Type t)
        {
            if(t == null)
            {
                throw new ArgumentNullException("t");
            }  

            var result = new List<Tuple<Type, IEnumerable<FieldInfo>>>();
            foreach(var type in t.GetInheritanceHierarchy())
            {
                result.Add(Tuple.Create(type, GetAllFields(type, false)));
            }
            return result;
        }

        public static IEnumerable<Type> GetInheritanceHierarchy(this Type t)
        {
            if(t == null)
            {
                throw new ArgumentNullException("t");
            }  

            var result = new List<Type> { t };
            while(t.BaseType != null)
            {
                result.Insert(0, t.BaseType);
                t = t.BaseType;
            }

            return result;
        }

        public static FieldInfo GetFieldInfo<T, TResult>(Expression<Func<T, TResult>> expression)
        {
            var memberExpression = (MemberExpression)expression.Body;
            return (FieldInfo)memberExpression.Member;
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

        public static bool IsWriteableByPrimitiveWriter(Type type)
        {
            return TypesWriteableByPrimitiveWriter.Contains(type);
        }

        public static bool IsWriteableByPrimitiveWriter(TypeDescriptor type)
        {
            return TypesWriteableByPrimitiveWriterAQNs.Contains(type.AssemblyQualifiedName);
        }

        internal static bool CheckTransientNoCache(Type type)
        {
            return type.IsDefined(typeof(TransientAttribute), true);
        }

        internal static SerializationType GetSerializationType(Type type)
        {
            if(Helpers.CheckTransientNoCache(type))
            {
                return SerializationType.Transient;
            }
            // treat pointer as a value type so that it is immediately check
            // for legality (which should fail)
            if(type.IsValueType || type.IsPointer)
            {
                return SerializationType.Value;
            }
            return SerializationType.Reference;
        }

        internal static int GetSurrogateFactoryIdForType(Type type, InheritanceAwareList<Delegate> swapList)
        {
            var i = 0;
            foreach(var swapCandidate in swapList)
            {
                if(swapCandidate.Key.IsAssignableFrom(type))
                {
                    return swapCandidate.Value == null ? -1 : i;
                }
                i++;
            }
            return -1;
        }

        internal static int[] AllIndicesOf(this string str, char c)
        {
            var result = new List<int>();
            var array = str.ToCharArray();
            for(int i = 0; i < array.Length; i++)
            {
                if(array[i] == c)
                {
                    result.Add(i);
                }
            }
            return result.ToArray();
        }

        internal static int IndexOfOccurence(this string str, char c, int occurence)
        {
            var indices = str.AllIndicesOf(c);
            if ((occurence < 0 && indices.Length <= -occurence) || (occurence >= 0 && indices.Length < occurence))
            {
                return -1;
            }
            return occurence < 0 ? indices[indices.Length + occurence] : indices[occurence];
        }

        internal static readonly DateTime DateTimeEpoch = new DateTime(2000, 1, 1);

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

        static Helpers()
        {
            TypesWriteableByPrimitiveWriter = new HashSet<Type>(typeof(PrimitiveWriter).GetMethods().Where(x => x.Name == "Write").Select(x => x.GetParameters()[0].ParameterType));
            TypesWriteableByPrimitiveWriterAQNs = new HashSet<string>(TypesWriteableByPrimitiveWriter.Select(x => x.AssemblyQualifiedName));
            TypesWriteableByPrimitiveWriter.TrimExcess();
            TypesWriteableByPrimitiveWriterAQNs.TrimExcess();
        }

        private static readonly HashSet<Type> TypesWriteableByPrimitiveWriter;
        // this collection is here only to speed up lookups of `TypeDescriptor` objects
        private static readonly HashSet<string> TypesWriteableByPrimitiveWriterAQNs;
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
