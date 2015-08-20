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

namespace Antmicro.Migrant.VersionTolerance
{
    public class ArrayDescriptor
    {
        public static ArrayDescriptor EmptyRanks { get { return new ArrayDescriptor(typeof(void), new int[0]); } }

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
    }
}

