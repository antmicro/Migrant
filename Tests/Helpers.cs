/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

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
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Migrantoid.Tests
{
    public static class Helpers
    {
        public static string GetRandomString(int length)
        {
            var builder = new StringBuilder(length);
            for(var i = 0; i < length; i++)
            {
                char character;
                do
                {
                    character = (char)Random.Next(0xFF + 1);
                }
                while(char.IsControl(character));
                builder.Append(character);
            }
            return builder.ToString();
        }

        public static int[] GetRandomIntegers(int numberOfInts, int exclusiveMaximum = int.MaxValue)
        {
            var randomInts = new int[numberOfInts];
            for(var i = 0; i < randomInts.Length; i++)
            {
                randomInts[i] = Random.Next(exclusiveMaximum) - Random.Next(exclusiveMaximum);
            }
            return randomInts;
        }

        public static long[] GetRandomLongs(int numberOfLongs, long exclusiveMaximum = long.MaxValue)
        {
            int lowMax, hiMax;
            if(exclusiveMaximum > int.MaxValue)
            {
                lowMax = int.MaxValue;
                hiMax = (int)((exclusiveMaximum >> 32) - 1);
            }
            else
            {
                hiMax = 0;
                lowMax = (int)exclusiveMaximum;
            }
            var randomLongs = new long[numberOfLongs];
            for(var i = 0; i < randomLongs.Length; i++)
            {
                randomLongs[i] = ((long)Random.Next(hiMax) << 32) + Random.Next(lowMax);
            }
            return randomLongs;
        }

        public static double[] GetRandomDoubles(int numberOfDoubles, double exclusiveMaximum = double.MaxValue)
        {
            var randomDoubles = new double[numberOfDoubles];
            for(var i = 0; i < randomDoubles.Length; i++)
            {
                randomDoubles[i] = (Random.NextDouble() - 0.5) * exclusiveMaximum;
            }
            return randomDoubles;
        }

        public static decimal[] GetRandomDecimals(int numberOfDecimals)
        {
            var randomDecimals = new decimal[numberOfDecimals];
            for(var i = 0; i < randomDecimals.Length; i++)
            {
                randomDecimals[i] = NextDecimal();
            }
            return randomDecimals;
        }

        public static TimeSpan[] GetRandomTimeSpans(int numberOfTimeSpans)
        {
            return GetRandomLongs(numberOfTimeSpans, TimeSpan.MaxValue.Ticks).Select(x => TimeSpan.FromTicks(x)).ToArray();
        }

        public static DateTime[] GetRandomDateTimes(int numberOfDateTimes)
        {
            var totallyRandomTimes = GetRandomLongs(numberOfDateTimes / 2, DateTime.MaxValue.Ticks).Select(x => new DateTime(x));
            var notSoRandomNo = numberOfDateTimes - numberOfDateTimes / 2;
            var notSoRandomTimes = new List<DateTime>();
            for(var i = 0; i < notSoRandomNo; i++)
            {
                notSoRandomTimes.Add(new DateTime(2000 + ZeroEvery(16) * Random.Next(100), 1 + ZeroEvery(16) * Random.Next(12), 1 + ZeroEvery(16) * Random.Next(28),
                    ZeroEvery(8) * Random.Next(24), ZeroEvery(4) * Random.Next(60), ZeroEvery(2) * Random.Next(60)));
            }
            return totallyRandomTimes.Concat(notSoRandomTimes).ToArray();
        }

        private static int ZeroEvery(int every)
        {
            return Random.Next(every) == 0 ? 0 : 1;
        }

        public static Random Random { get; private set; }

        private static decimal NextDecimal()
        {
            byte scale = (byte) Random.Next(29);
            bool sign = Random.Next(2) == 1;
            return new decimal(NextInt(),
                NextInt(),
                NextInt(),
                sign,
                scale);
        }

        private static int NextInt()
        {
            return (((Random.Next(int.MinValue, int.MaxValue)) & 0xFFFF) << 16) | ((Random.Next(int.MinValue, int.MaxValue)) & 0xFFFF);
        }

        static Helpers()
        {
            Random = new Random();
        }
    }
}
