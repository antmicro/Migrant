using System;
using System.Collections.Generic;
using ImpromptuInterface;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AntMicro.Migrant
{
    internal static class Helpers
    {
        internal static bool TryGetCollectionCountAndElementType(object o, out int count, out Type formalElementType)
        {
            if(IsCollection(o.GetType(), out formalElementType))
            {
                count = (int)Impromptu.InvokeGet(o, "Count");
                return true;
            }
            count = -1;
            return false;
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

        public static bool IsCollection(Type actualType, out Type formalElementType)
        {
            formalElementType = typeof(object);
            var ifaces = actualType.GetInterfaces();
            var result = false;
            foreach(var iface in ifaces)
            {
                if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    formalElementType = iface.GetGenericArguments()[0];
                    return true;
                }
                if(iface == typeof(ICollection))
                {
                    result = true;
                }
                if(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    formalElementType = iface.GetGenericArguments()[0];
                }
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
            return actualType == typeof(string) || actualType.IsValueType || actualType.IsArray;
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

        public static void InvokeAttribute(Type attributeType, object o)
        {
            var methodsToInvoke = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(x => x.IsDefined(attributeType, false));
            foreach(var method in methodsToInvoke)
            {
                method.Invoke(o, new object[0]);
            }
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

        public static readonly DateTime DateTimeEpoch = new DateTime(2000, 1, 1);

        private static readonly int[] PaddingBoundaries = new [] { 128, 1024, 4096, int.MaxValue };

		private const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | 
                BindingFlags.Instance | BindingFlags.DeclaredOnly;
    }
}

