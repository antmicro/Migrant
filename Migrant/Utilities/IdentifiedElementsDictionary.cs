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
using System.Collections.Generic;

namespace Antmicro.Migrant.Utilities
{
    internal class IdentifiedElementsDictionary<T> where T : IIdentifiedElement
    {
        public IdentifiedElementsDictionary(ObjectWriter writer)
        {
            this.writer = writer;
            Dictionary = new Dictionary<T, int>();
        }

        public int TouchAndWriteId(T element)
        {
            int typeId;
            if(Dictionary.TryGetValue(element, out typeId))
            {
                writer.PrimitiveWriter.Write(typeId);
                return typeId;
            }
            typeId = AddAndAdvanceId(element);
            writer.PrimitiveWriter.Write(typeId);
            element.Write(writer);
            return typeId;
        }

        public int AddAndAdvanceId(T element)
        {
            var typeId = nextId++;
            Dictionary.Add(element, typeId);
            return typeId;
        }

        public Dictionary<T, int> Dictionary { get; private set; }

        private int nextId;
        private readonly ObjectWriter writer;
    }
}

