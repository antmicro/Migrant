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
using System.Collections.Concurrent;

namespace Antmicro.Migrant
{
    internal static class Helpers
    {
        public static bool CanBeCreatedWithDataOnly(Type actualType)
        {
            return actualType == typeof(string) || actualType.IsValueType || actualType.IsArray || typeof(MulticastDelegate).IsAssignableFrom(actualType);
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
            return recursive ? t.GetFields(DefaultBindingFlags).Union(GetAllFields(t.BaseType)) : t.GetFields(DefaultBindingFlags);
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
                throw new ArgumentException("Expression does not point to class member.");
            }

            var pinfo = mexpr.Member as PropertyInfo;
            if(pinfo == null)
            {
                throw new ArgumentException("Expression does not point to a property.");
            }

            return pinfo.GetGetMethod(true);
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

        public static MethodInfo GetImplicitConvertionOperatorInfo<TTo, TFrom>()
        {
            return typeof(TFrom).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(x => x.Name == "op_Implicit" && x.GetParameters().Count() == 1 && x.GetParameters().ElementAt(0).ParameterType == typeof(TTo));
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

        public static ConstructorInfo GetConstructorInfo<T>(params Type[] argumentsTypes)
        {
            var result = typeof(T).GetConstructor(argumentsTypes);
            if(result == null)
            {
                throw new ArgumentException("Constructor not found.");
            }
            return result;
        }

        public static bool IsWriteableByPrimitiveWriter(Type type)
        {
            return TypesWriteableByPrimitiveWriter.Contains(type);
        }

        internal static bool IsTransient(Type type)
        {
            return AttrbuteIsDefinedLookup.GetOrAdd(type, t => t.IsDefined(typeof(TransientAttribute), true));
        }

        internal static bool IsTransient(object o)
        {
            return IsTransient(o.GetType());
        }

        internal static SerializationType GetSerializationType(Type type)
        {
            if(IsTransient(type))
            {
                return SerializationType.Transient;
            }
            // Pointer is considered a value type just to
            // make it verified and filtered-out later.
            if(type.IsValueType || type.IsPointer)
            {
                return SerializationType.Value;
            }
            return SerializationType.Reference;
        }

        internal enum TypeOfGenericType
        {
            // The type is not generic at all
            NonGenericType,
            // The type is generic and none of its generic arguments is set
            OpenGenericType,
            // The type is an internal type of other generic type and all its generic arguments are set, but some of them
            // points to generic arguments of parent type
            FixedNestedGenericType,
            // The type is an internal type of other generic type and some of its generic arguments points to generic
            // arguments of parent type, the rest is not set
            PartiallyFixedNestedGenericType,
            // The type is generic and all of its generic arguments are set
            ClosedGenericType
        }

        internal static TypeOfGenericType GetTypeOfGenericType(Type type)
        {
            if(!type.IsGenericType)
            {
                return TypeOfGenericType.NonGenericType;
            }

            var hasGenericArgumentsPointingToParent = false;
            var hasGenericArgumentsNotPointingToParent = false;
            foreach(var genericParameter in type.GetGenericArguments())
            {
                if(genericParameter.IsGenericParameter)
                {
                    if(genericParameter.DeclaringType == type)
                    {
                        hasGenericArgumentsNotPointingToParent = true;
                    }
                    else
                    {
                        hasGenericArgumentsPointingToParent = true;
                    }
                }
            }

            if(hasGenericArgumentsPointingToParent)
            {
                if(hasGenericArgumentsNotPointingToParent)
                {
                    return TypeOfGenericType.PartiallyFixedNestedGenericType;
                }
                else
                {
                    return TypeOfGenericType.FixedNestedGenericType;
                }
            }
            else
            {
                if(hasGenericArgumentsNotPointingToParent)
                {
                    return TypeOfGenericType.OpenGenericType;
                }
                else
                {
                    return TypeOfGenericType.ClosedGenericType;
                }
            }
        }

        internal static bool ContainsGenericArguments(Type type)
        {
            if(type.IsArray)
            {
                return ContainsGenericArguments(type.GetElementType());
            }
            return type.ContainsGenericParameters;
        }

        internal static void WriteArray(this PrimitiveWriter writer, int[] elements)
        {
            writer.Write(elements.Count());
            foreach(var element in elements)
            {
                writer.Write(element);
            }
        }

        internal static int[] ReadArray(this PrimitiveReader reader)
        {
            var result = new int[reader.ReadInt32()];
            for(int i = 0; i < result.Length; i++)
            {
                result[i] = reader.ReadInt32();
            }
            return result;
        }

        internal static bool IsTypeWritableDirectly(Type fieldType)
        {
            return IsWriteableByPrimitiveWriter(fieldType) || (GetSerializationType(fieldType) == SerializationType.Value);
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
            TypesWriteableByPrimitiveWriter = new HashSet<Type>(
                typeof(PrimitiveWriter).GetMethods().Where(x => x.Name == "Write").Select(x => x.GetParameters()[0].ParameterType).Where(x => x != typeof(string) && x != typeof(byte[])));
            TypesWriteableByPrimitiveWriter.TrimExcess();
            AttrbuteIsDefinedLookup = new ConcurrentDictionary<Type, bool>();
        }

        private static readonly ConcurrentDictionary<Type, bool> AttrbuteIsDefinedLookup;
        private static readonly HashSet<Type> TypesWriteableByPrimitiveWriter;
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
