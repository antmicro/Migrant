//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;

namespace Antmicro.Migrant.Utilities
{
    internal class IdentifiedElementsList<T> where T : IIdentifiedElement, new()
    {
        public IdentifiedElementsList(ObjectReader reader)
        {
            list = new List<T>();
            this.reader = reader;
        }

        public T Read()
        {
            var id = reader.PrimitiveReader.ReadInt32();
            if(id == Consts.NullObjectId)
            {
                return default(T);
            }
            if(list.Count <= id)
            {
                var element = new T(); 
                list.Add(element);
                element.Read(reader);
                return element;
            }

            return list[id];
        }

        public void Clear()
        {
            list.Clear();
        }

        private readonly ObjectReader reader;
        private readonly List<T> list;
    }
}

