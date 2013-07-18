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
using AntMicro.Migrant.Customization;

namespace AntMicro.Migrant.VersionTolerance
{
	internal sealed class TypeStampReader
	{
		public TypeStampReader(PrimitiveReader reader, VersionToleranceLevel versionToleranceLevel)
		{
			this.reader = reader;
			this.versionToleranceLevel = versionToleranceLevel;
			stampCache = new Dictionary<Type, List<FieldInfoOrEntryToOmit>>();
		}

		public void ReadStamp(Type type)
		{
			if(!StampHelpers.IsStampNeeded(type))
			{
				return;
			}
			if(stampCache.ContainsKey(type))
			{
				return;
			}
			var result = new List<FieldInfoOrEntryToOmit>();
			var currentFields = StampHelpers.GetFieldsInSerializationOrder(type, true).ToDictionary(x => x.Name, x => x);
			var fieldNo = reader.ReadInt32();
			for(var i = 0; i < fieldNo; i++)
			{
				var fieldName = reader.ReadString();
				var fieldType = Type.GetType(reader.ReadString());
				FieldInfo currentField;
				if(!currentFields.TryGetValue(fieldName, out currentField))
				{
					// field is missing in the newer version of the class
					// we have to read the data to properly move stream,
					// but that's all - unless this is illegal from the
					// version tolerance level point of view
					if(versionToleranceLevel != VersionToleranceLevel.FieldRemoval && versionToleranceLevel != VersionToleranceLevel.FieldAdditionAndRemoval)
					{
						throw new InvalidOperationException(string.Format("Field {0} of type {1} was present in the old version of the class but is missing now. This is" +
						                                                  "incompatible with selected version tolerance level which is {2}.", fieldName, fieldType,
						                                                  versionToleranceLevel));
					}
					result.Add(new FieldInfoOrEntryToOmit(fieldType));
					continue;
				}
				// are the types compatible?
				if(currentField.FieldType != fieldType)
				{
					throw new InvalidOperationException(string.Format("Field {0} was of type {1} in the old version of the class, but currently is {2}. Cannot proceed.",
					                                                  fieldName, fieldType, currentField.FieldType));
				}
				// why do we remove a field from current ones? if some field is still left after our operation, then field addition occured
				// we have to check that, cause it can be illegal from the version tolerance point of view
				currentFields.Remove(fieldName);
				result.Add(new FieldInfoOrEntryToOmit(currentField));
			}
			// result should also contain transient fields, because some of them may
			// be marked with the [Constructor] attribute
			var transientFields = currentFields.Select(x => x.Value).Where(x => !Helpers.IsNotTransient(x)).Select(x => new FieldInfoOrEntryToOmit(x)).ToArray();
			if(currentFields.Count - transientFields.Length > 0 && versionToleranceLevel != VersionToleranceLevel.FieldAddition && 
			   versionToleranceLevel != VersionToleranceLevel.FieldAdditionAndRemoval)
			{
				throw new InvalidOperationException(string.Format("Current version of the class {0} contains more fields than it had when it was serialized. With given" +
				                                                  "version tolerance level {1} the serializer cannot proceed.", type, versionToleranceLevel));
			}
			result.AddRange(transientFields);
			stampCache.Add(type, result);
		}

		public IEnumerable<FieldInfoOrEntryToOmit> GetFieldsToDeserialize(Type type)
		{
			return stampCache[type];
		}

		private readonly Dictionary<Type, List<FieldInfoOrEntryToOmit>> stampCache;
		private readonly PrimitiveReader reader;
		private readonly VersionToleranceLevel versionToleranceLevel;
	}
}

