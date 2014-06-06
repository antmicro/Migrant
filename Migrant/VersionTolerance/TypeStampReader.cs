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
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant.VersionTolerance
{
	internal sealed class TypeStampReader
	{
		public TypeStampReader(PrimitiveReader reader, VersionToleranceLevel versionToleranceLevel)
		{
			this.reader = reader;
			this.versionToleranceLevel = versionToleranceLevel;
			stampCache = new Dictionary<Type, List<FieldInfoOrEntryToOmit>>();
		}

        public void ReadStamp(Type type, bool treatCollectionAsUserObject)
		{
            if(!StampHelpers.IsStampNeeded(type, treatCollectionAsUserObject))
			{
				return;
			}
			if(stampCache.ContainsKey(type))
			{
				return;
			}
            var moduleGuid = reader.ReadGuid(); // module GUID
            var classNo = reader.ReadInt32(); // # of classes
			if(moduleGuid == type.Module.ModuleVersionId)
			{
				// short path, nothing to check
                for(var i = 0; i < classNo; i++)
				{
                    var s = reader.ReadString(); // class type (AQN)
                    var fieldNo = reader.ReadInt32(); // # of fields in the class

                    for(var j = 0; j < fieldNo; j++)
                    {
                        var fn = reader.ReadString(); // field name
                        var ft = reader.ReadString(); // field type (AQN)
                    }
				}
				stampCache.Add(type, StampHelpers.GetFieldsInSerializationOrder(type, true).Select(x => new FieldInfoOrEntryToOmit(x)).ToList());
				return;
			}
			if(versionToleranceLevel == VersionToleranceLevel.Guid)
			{
				throw new InvalidOperationException(string.Format("The class was serialized with different module version id {0}, current one is {1}.",
				                                                  moduleGuid, type.Module.ModuleVersionId));
			}

            var result = new List<FieldInfoOrEntryToOmit>();
            var currentClasses = StampHelpers.GetFieldsStructureInSerializationOrder(type, true).ToList();
            for(var i = 0; i < classNo; i++)
            {
                var expectedClass = currentClasses[i];
                var className = reader.ReadString(); // class name (AQN)
                var fieldNo = reader.ReadInt32(); // # of field in the class

                if(className != expectedClass.Item1.AssemblyQualifiedName)
                {
                    throw new InvalidOperationException(string.Format("Class hierarchy changed. Expected {0}, but found {1}.", expectedClass.Item1.AssemblyQualifiedName, className));
                }

                var currentFields = expectedClass.Item2.ToDictionary(x => x.Name, x => x);
                for(var j = 0; j < fieldNo; j++)
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
            }

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

