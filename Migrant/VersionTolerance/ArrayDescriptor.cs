//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;

namespace Antmicro.Migrant.VersionTolerance
{
    internal class ArrayDescriptor
    {
        public static ArrayDescriptor EmptyRanks { get; private set; }

        public ArrayDescriptor(Type type)
        {
            var ranks = new List<int>();
            var current = type;
            do
            {
                var currentRank = current.GetArrayRank();
                ranks.Insert(0, currentRank);
                current = current.GetElementType();
            }
            while(current.IsArray);
            ElementType = current;
            Ranks = ranks.ToArray();
        }

        public ArrayDescriptor(Type elementType, int[] ranks)
        {
            ElementType = elementType;
            Ranks = ranks;
        }

        public Type BuildArrayType()
        {
            var elementType = ElementType;
            foreach(var rank in Ranks)
            {
                elementType = rank == 1 ? elementType.MakeArrayType() : elementType.MakeArrayType(rank);
            }
            return elementType;
        }

        public Type ElementType { get; private set; }
        public int[] Ranks { get; private set; }

        static ArrayDescriptor()
        {
            EmptyRanks = new ArrayDescriptor(typeof(void), new int[0]);
        }
    }
}

