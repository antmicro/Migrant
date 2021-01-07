//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;

namespace Antmicro.Migrant.Utilities
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

