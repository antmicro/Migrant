/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)

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
using System.Text;

namespace Antmicro.Migrant.VersionTolerance
{
	internal static class StampHelpers
	{
        public static bool IsStampNeeded(Type type, bool treatCollectionAsUserObject)
		{
            return !Helpers.IsWriteableByPrimitiveWriter(type) && (!new CollectionMetaToken(type).IsCollection || treatCollectionAsUserObject);
		}

		public static IEnumerable<FieldInfo> GetFieldsInSerializationOrder(Type type, bool withTransient = false)
		{
            return GetFieldsStructureInSerializationOrder(type, withTransient).SelectMany(x => x.Item2);
		}

        public static IEnumerable<Tuple<Type, IEnumerable<FieldInfo>>> GetFieldsStructureInSerializationOrder(Type type, bool withTransient = false)
        {
            return type.GetAllFieldsStructurized().Select(x => Tuple.Create(x.Item1, x.Item2.Where(y => withTransient || Helpers.IsNotTransient(y)).OrderBy(y => y.Name).AsEnumerable()));
        }

        public static void ToPrettyString(this TypeStamp.TypeStampCompareResult results)
        {
            Console.WriteLine("Fields added:");
            foreach (var f in results.FieldsAdded)
            {
              Console.WriteLine("\t" + f.Name);
            }
            Console.WriteLine("Fields removed:");
            foreach (var f in results.FieldsRemoved)
            {
              Console.WriteLine("\t" + f.Name);
            }
            Console.WriteLine("Fields moved:");
            foreach (var f in results.FieldsMoved)
            {
              Console.WriteLine("\t" + f.Key.Name + " " + f.Key.OwningTypeAQN + " => " + f.Value.OwningTypeAQN);
            }
            Console.WriteLine("Fields changed:");
            foreach (var f in results.FieldsChanged)
            {
              Console.WriteLine("\t" + f.Name);
            }
        }

        public static string ToPrettyString(this TypeDescriptor type)
        {
            var bldr = new StringBuilder();
            bldr.AppendFormat("Type name: {0}\n", type.AssemblyQualifiedName);
            foreach(var f in type.Fields)
            {
                bldr.Append(f);
            }
            return bldr.ToString();
        }

        public static string ToPrettyString(this TypeStamp stamp)
        {
            var bldr = new StringBuilder();
            bldr.AppendLine("-----------------");
            bldr.AppendFormat("GUID: {0}\n", stamp.ModuleGUID);
            foreach(var c in stamp.Classes)
            {
                bldr.Append(c).AppendLine();
            }
            bldr.AppendLine("-----------------");
            return bldr.ToString();
        }

        public static string ToPrettyString(this FieldDescriptor field)
        { 
            var bldr = new StringBuilder();
            bldr.AppendFormat("Name: {0}\n", field.Name);
            bldr.AppendFormat("Type: {0}\n", field.TypeAQN);
            bldr.AppendFormat("Owned by: {0}\n", field.OwningTypeAQN);
            return bldr.ToString();
        }
	}
}

