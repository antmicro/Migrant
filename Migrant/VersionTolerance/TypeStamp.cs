// *******************************************************************
//
//  Copyright (c) 2012-2014, Antmicro Ltd <antmicro.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// *******************************************************************
using System;
using System.Collections.Generic;
using Antmicro.Migrant.VersionTolerance;
using System.Linq;
using System.Diagnostics;
using System.Text;

namespace Antmicro.Migrant
{
  public class TypeStamp
  {
    public TypeStamp()
    {
      Classes = new List<TypeDescriptor>();
    }

    public TypeStamp(Type type, bool withTransient = false) : this()
    {
      var structure = StampHelpers.GetFieldsStructureInSerializationOrder(type, withTransient);
      ModuleGUID = type.Module.ModuleVersionId;

      foreach(var cl in structure)
      {
        Classes.Add(new TypeDescriptor(cl));
      }
    }

    public void WriteTo(PrimitiveWriter writer)
    {
      writer.Write(ModuleGUID); 
      writer.Write(Classes.Count); 
      foreach(var cl in Classes)
      {
        cl.WriteTo(writer);
      }
    }

    public void ReadFrom(PrimitiveReader reader)
    {
      ModuleGUID = reader.ReadGuid();
      var classNo = reader.ReadInt32();
      for(var j = 0; j < classNo; j++)
      {
        var td = new TypeDescriptor();
        td.ReadFrom(reader);
        Classes.Add(td);
      }
    }

    public TypeStampCompareResult CompareWith(TypeStamp previous)
    {
      var result = new TypeStampCompareResult();

      var lastMatchedIndex = -1;
      var currOffset = 0;
      foreach (var cl in Classes)
      {
        // try find class with the same name 
        var matchedIndex = previous.IndexOfClass(c => c.AssemblyQualifiedName == cl.AssemblyQualifiedName);
        if (matchedIndex == -1)
        {
          // class was not found
          // we can try to find class with exact same layout and assume that it was renamed
          matchedIndex = previous.IndexOfClass(c => c.HasSameLayout(cl) && !Classes.Any(x => x.AssemblyQualifiedName == c.AssemblyQualifiedName));
          if (matchedIndex == -1) 
          {
            // we assume that this class is removed base one
            //result.Add (new TypeStampDiff (TypeStampDiffKind.BaseClassAdded, cl));
            result.ClassesAdded.Add(cl.AssemblyQualifiedName);
            currOffset++;
            continue;
          }

          result.ClassesRenamed.Add(Tuple.Create(previous.Classes[matchedIndex].AssemblyQualifiedName, cl.AssemblyQualifiedName));
        }

        if (matchedIndex == Classes.IndexOf(cl)) {
          var compareResult = cl.CompareWith(previous.Classes [matchedIndex]);
          result.FieldsAdded.AddRange(compareResult.FieldsAdded);
          result.FieldsRemoved.AddRange(compareResult.FieldsRemoved);
          result.FieldsChanged.AddRange(compareResult.FieldsChanged);
          lastMatchedIndex = matchedIndex;
        }
        else if (matchedIndex > Classes.IndexOf(cl) + currOffset)
        {
          // the new base class has been introduced
          for (var i = lastMatchedIndex + 1; i < matchedIndex; i++)
          {
            result.ClassesRemoved.Add(previous.Classes[i].AssemblyQualifiedName);
          }
          lastMatchedIndex = matchedIndex;
        }
      }

      result.CheckFieldMove();
      return result;
    }

    public int IndexOfClass(Func<TypeDescriptor, bool> selector)
    {
      for (var i = 0; i < Classes.Count; i++) 
      {
        if (selector(Classes[i]))
        {
          return i;
        }
      }
      return -1;
    }

    public IEnumerable<FieldDescriptor> GetFieldsInAlphabeticalOrder()
    {
      return Classes.SelectMany(x => x.Fields.OrderBy(y => y.Name));
    }

    public override string ToString()
    {
            var bldr = new StringBuilder();
            bldr.AppendLine("-----------------");
            bldr.AppendFormat("GUID: {0}\n", ModuleGUID);
            foreach(var c in Classes)
            {
                bldr.Append(c).AppendLine();
            }
            bldr.AppendLine("-----------------");
            return bldr.ToString();
    }

    public Guid ModuleGUID { get; private set; }
    public List<TypeDescriptor> Classes { get; private set; }

    public class TypeStampCompareResult
    {
      public List<FieldDescriptor> FieldsAdded   { get; private set; }
      public List<FieldDescriptor> FieldsRemoved { get; private set; }
      public Dictionary<FieldDescriptor, FieldDescriptor> FieldsMoved { get; private set; }
      public List<FieldDescriptor> FieldsChanged { get; private set; }

      public List<Tuple<string, string>> ClassesRenamed { get; private set; }
      public List<string> ClassesAdded { get; private set; }
      public List<string> ClassesRemoved { get; private set; }

      public bool Empty 
      {
        get
        {
          return FieldsChanged.Count == 0 && FieldsAdded.Count == 0 
            && FieldsRemoved.Count == 0 && FieldsMoved.Count == 0 
            && ClassesRemoved.Count == 0 && ClassesAdded.Count == 0 
            && ClassesRenamed.Count == 0;
        }
      }

      public TypeStampCompareResult()
      {
        FieldsAdded = new List<FieldDescriptor>();
        FieldsRemoved = new List<FieldDescriptor>();
        FieldsMoved = new Dictionary<FieldDescriptor, FieldDescriptor>();
        FieldsChanged = new List<FieldDescriptor>();

        ClassesRenamed = new List<Tuple<string, string>>();
        ClassesAdded = new List<string>();
        ClassesRemoved = new List<string>();
      }

      public void CheckFieldMove()
      {
        var comparer = new FieldDescriptor.MoveFieldComparer();
        var moved = FieldsAdded.Intersect(FieldsRemoved, comparer).ToList();
        foreach (var m in moved)
        {
          var fAdded = FieldsAdded.Single(x => comparer.Equals(x, m));
          var fRemoved = FieldsRemoved.Single(x => comparer.Equals(x, m));
          FieldsMoved.Add(fRemoved, fAdded);
          FieldsAdded.Remove(fAdded);
          FieldsRemoved.Remove(fRemoved);
        }
      }

      [Conditional("DEBUG")]
      public void PrintSummary()
      {
        Console.WriteLine("Fields added:");
        foreach (var f in FieldsAdded)
        {
          Console.WriteLine("\t" + f.Name);
        }
        Console.WriteLine("Fields removed:");
        foreach (var f in FieldsRemoved)
        {
          Console.WriteLine("\t" + f.Name);
        }
        Console.WriteLine("Fields moved:");
        foreach (var f in FieldsMoved)
        {
          Console.WriteLine("\t" + f.Key.Name + " " + f.Key.OwningTypeAQN + " => " + f.Value.OwningTypeAQN);
        }
        Console.WriteLine("Fields changed:");
        foreach (var f in FieldsChanged)
        {
          Console.WriteLine("\t" + f.Name);
        }
      }
    }
  }
}
