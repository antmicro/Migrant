//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

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

        public void Clear()
        {
            Dictionary.Clear();
            nextId = 0;
        }

        public Dictionary<T, int> Dictionary { get; private set; }

        private int nextId;
        private readonly ObjectWriter writer;
    }
}

