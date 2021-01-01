// *******************************************************************
//
//  Copyright (c) 2012-2016, Antmicro Ltd <antmicro.com>
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

namespace Migrantoid.Utilities
{
    internal class OneToOneMap<TOne, TTwo>
    {
        public OneToOneMap()
        {
            oneToTwo = new Dictionary<TOne, TTwo>();
            twoToOne = new Dictionary<TTwo, TOne>();
        }

        public void Add(TOne one, TTwo two)
        {
            oneToTwo.Add(one, two);
            twoToOne.Add(two, one);
        }

        public void Clear()
        {
            oneToTwo.Clear();
            twoToOne.Clear();
        }

        public bool TryGetValue(TOne one, out TTwo two)
        {
            return oneToTwo.TryGetValue(one, out two);
        }

        public bool ContainsKey(TOne one)
        {
            return oneToTwo.ContainsKey(one);
        }

        public TTwo this[TOne one]
        {
            get
            {
                return oneToTwo[one];
            }

            set
            {
                Remove(one);

                oneToTwo[one] = value;
                twoToOne[value] = one;
            }
        }

        public TOne this[TTwo two]
        {
            get
            {
                return twoToOne[two];
            }

            set
            {
                twoToOne.Remove(two);
                oneToTwo.Remove(twoToOne[two]);

                oneToTwo[value] = two;
                twoToOne[two] = value;
            }
        }

        public void Remove(TOne one)
        {
            twoToOne.Remove(oneToTwo[one]);
            oneToTwo.Remove(one);
        }

        public int Count { get { return oneToTwo.Count; } }

        private Dictionary<TOne, TTwo> oneToTwo;
        private Dictionary<TTwo, TOne> twoToOne;
    }
}

