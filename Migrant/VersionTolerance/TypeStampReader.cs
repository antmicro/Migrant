/*
   Copyright (c) 2013 Ant Micro <www.antmicro.com>

 Authors:
  * Konrad Kruczynski (kkruczynski@antmicro.com)
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

      var streamTypeStamp = new TypeStamp();
      streamTypeStamp.ReadFrom(reader);

      if(streamTypeStamp.ModuleGUID == type.Module.ModuleVersionId)
      {
        stampCache.Add(type, StampHelpers.GetFieldsInSerializationOrder(type, true).Select(x => new FieldInfoOrEntryToOmit(x)).ToList());
        return;
      }
      if(versionToleranceLevel == VersionToleranceLevel.Guid)
      {
        throw new InvalidOperationException(string.Format("The class was serialized with different module version id {0}, current one is {1}.",
              streamTypeStamp.ModuleGUID, type.Module.ModuleVersionId));
      }

      var result = new List<FieldInfoOrEntryToOmit>();
      var assemblyTypeStamp = new TypeStamp(type, true);
      if (assemblyTypeStamp.Classes.Count != streamTypeStamp.Classes.Count && !versionToleranceLevel.HasFlag(VersionToleranceLevel.InheritanceChainChange))
      {
        throw new InvalidOperationException(string.Format("Class hierarchy changed. Expected {0} classes in a chain, but found {1}.", assemblyTypeStamp.Classes.Count, streamTypeStamp.Classes.Count));
      }

      var cmpResult = assemblyTypeStamp.CompareWith(streamTypeStamp);

      if (cmpResult.ClassesRenamed.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.TypeNameChanged))
      {
        throw new InvalidOperationException(string.Format("Class name changed from {0} to {1}", cmpResult.ClassesRenamed[0].Item1, cmpResult.ClassesRenamed[0].Item2));
      }
      if (cmpResult.FieldsAdded.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.FieldAddition))
      {
        throw new InvalidOperationException(string.Format("Field added: {0}.", cmpResult.FieldsAdded[0].Name));
      }
      if (cmpResult.FieldsRemoved.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.FieldRemoval))
      {
        throw new InvalidOperationException(string.Format("Field removed: {0}.", cmpResult.FieldsRemoved[0].Name));
      }
      if (cmpResult.FieldsMoved.Any() && !versionToleranceLevel.HasFlag(VersionToleranceLevel.FieldMove))
      {
        throw new InvalidOperationException(string.Format("Field moved: {0}.", cmpResult.FieldsMoved.ElementAt(0).Key.Name));
      }

      cmpResult.PrintSummary();
      foreach (var field in streamTypeStamp.GetFieldsInAlphabeticalOrder()) 
      {
        if (cmpResult.FieldsRemoved.Contains(field))
        {
          result.Add(new FieldInfoOrEntryToOmit(Type.GetType(field.TypeAQN)));
        }
        else if (cmpResult.FieldsMoved.ContainsKey(field))
        {
          var mvd = cmpResult.FieldsMoved[field];
          var ttt = Type.GetType(mvd.OwningTypeAQN);
          var finfo = ttt.GetField(mvd.Name);
          result.Add(new FieldInfoOrEntryToOmit(finfo));
        }
        else
        {
          var ttt = Type.GetType(field.OwningTypeAQN);
          var finfo = ttt.GetField(field.Name);
          result.Add(new FieldInfoOrEntryToOmit(finfo));
        }
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

