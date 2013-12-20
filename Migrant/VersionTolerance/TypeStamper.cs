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

namespace Antmicro.Migrant.VersionTolerance
{
	internal sealed class TypeStamper
	{
        public TypeStamper(PrimitiveWriter writer, bool treatCollectionAsUserObject)
		{
			this.writer = writer;
            this.treatCollectionAsUserObject = treatCollectionAsUserObject;
			alreadyWritten = new HashSet<Type>();
		}

		public void Stamp(Type type)
		{
            if(!StampHelpers.IsStampNeeded(type, treatCollectionAsUserObject))
			{
				return;
			}
			if(alreadyWritten.Contains(type))
			{
				return;
			}
			alreadyWritten.Add(type);
			var fields = StampHelpers.GetFieldsInSerializationOrder(type).ToArray();
			writer.Write(fields.Length);
			var moduleGuid = type.Module.ModuleVersionId;
			writer.Write(moduleGuid);
			foreach(var field in fields)
			{
				var fieldType = field.FieldType;
				writer.Write(field.Name);
				writer.Write(fieldType.AssemblyQualifiedName);
			}
		}

        private readonly bool treatCollectionAsUserObject;
		private readonly PrimitiveWriter writer;
		private readonly HashSet<Type> alreadyWritten;
	}
}

