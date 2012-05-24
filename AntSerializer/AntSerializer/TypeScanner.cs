using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Text;

namespace AntMicro.AntSerializer
{
    public class TypeScanner
    {
        public TypeScanner()
        {
            types = new Type[0];
        }

        public void Scan(Type typeToScan)
        {
            var typeSet = new HashSet<Type>();
            var typeStack = new Stack<Type>();
            ScanRecursiveWithStack(typeSet, typeToScan, typeStack);
            types = types.Union(typeSet).Distinct().ToArray();
        }

        public Type[] GetTypeArray()
        {
            return (Type[])types.Clone();
        }

        // TODO: what is that?
        //IsNull?
        //Throw on illegal
        //GenericParameters (not optimal before IsSerialized, it's here because of generic interfaces)
        //IsSerializable?
        //IsSerialized?
        //IsPrimitive || ValueType?
        //ShouldFieldsBeScanned?

        private static void ScanRecursiveWithStack(HashSet<Type> typeSet, Type typeToScan, Stack<Type> typeStack)
        {
            typeStack.Push(typeToScan);
            ScanRecursive(typeSet, typeToScan, typeStack);
            typeStack.Pop();
        }

        private static void ScanRecursive(HashSet<Type> typeSet, Type typeToScan, Stack<Type> typeStack)
        {
            if(typeToScan == null)
            {
                return;
            }
            if(typeof(ISpeciallySerializable).IsAssignableFrom(typeToScan) || typeToScan.IsDefined(typeof(TransientAttribute), false))
            {
                typeSet.Add(typeToScan);
            }
            BreakOnIllegalType(typeToScan, typeStack);
            foreach(var type in GetElementTypes(typeToScan))
            {
                ScanRecursiveWithStack(typeSet, type, typeStack);
            }
            if(!IsSerializable(typeToScan) || !typeSet.Add(typeToScan))
            {
                return;
            }
            if(!IsPrimitive(typeToScan) && !typeToScan.IsValueType)
            { //cannot add IsValueType to IsPrimitive, because it's used by ShouldFieldsBeScanned
                ScanRecursiveWithStack(typeSet, typeToScan.BaseType, typeStack);
            }
            if(!ShouldFieldsBeScanned(typeToScan))
            {
                return;
            }
            //TODO: unified check
            var fields = typeToScan.GetAllFields(false).Where(x => !x.Attributes.HasFlag(FieldAttributes.Literal) && !x.IsTransient());
            var typesToAdd = fields.Select(x => x.FieldType).Where(x => !x.IsInterface)
                .Distinct();
            foreach(var type in typesToAdd)
            {
                ScanRecursiveWithStack(typeSet, type, typeStack);
            }
        }

        private static void BreakOnIllegalType(Type typeToScan, Stack<Type> stack)
        {
            if(IllegalTypes.Contains(typeToScan) || typeToScan.IsPointer)
            {
                var revStack = stack.Reverse();
                var path = new StringBuilder(revStack.First().Name);
                foreach(var elem in revStack.Skip(1))
                {
                    path.Append(" -> ").Append(elem.Name);
                }
                throw new ArgumentException(String.Format(
                    "Type {0} is not serializable. Consider adding transient attribute.\nThe path to this type is:\n{1}",
                    typeToScan, path.ToString()
                )
                );
            }
        }

        private static bool IsPrimitive(Type type)
        {
            return type.IsPrimitive || type.IsEnum;
        }

        private static bool IsSerializable(Type type)
        {
            return !type.IsInterface; //no double checks
        }

        private static bool ShouldFieldsBeScanned(Type type)
        {
            if(IsPrimitive(type) 
                || type.GetInterfaces().Any(x =>
                                        (x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>)) 
                || x == typeof(ICollection)
            ))
            {
                return false;
            }
            return true;
        }

        private static IEnumerable<Type> GetElementTypes(Type type)
        {
            if(type.IsArray)
            {
                return new [] { type.GetElementType() };
            }
            if(type.IsGenericType)
            {
                return type.GetGenericArguments();
            }
            return new Type[0];
            // TODO: for collections, dictionaries and so on
        }

        private static HashSet<Type> IllegalTypes = new HashSet<Type>
        {
            typeof(IntPtr)
        };

        private Type[] types;
    }
}

