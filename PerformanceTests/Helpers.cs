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
using System.Collections.Generic;
using System.Linq;

namespace AntMicro.Migrant.PerformanceTests
{
	public static class Helpers
	{
		public static double StandardDeviation(this IEnumerable<double> values)
		{
			var result = 0.0;
			var valuesAsArray = values.ToArray();
			var count = valuesAsArray.Length;
			if(count > 1)
			{
				var average = valuesAsArray.Average();
				var sum = valuesAsArray.Sum(x => (x - average) * (x - average));
				result = Math.Sqrt(sum/count);
			}
			return result;
		}

		public static String NormalizeDecimal(this double what)
		{
			var prefix = (what < 0) ? "-" : "";
			if(what == 0)
			{
				return "0";
			}
			if(what < 0)
			{
				what = -what;
			}
			var digits = Convert.ToInt32(Math.Floor(Math.Log10(what)));
			var power = (long)(3 * Math.Round((digits / 3.0)));
			var index = power / 3 + ZeroPrefixPosition;
			if(index < 0)
			{
				index = 0;
				power = 3 * (1 + ZeroPrefixPosition - SIPrefixes.Length);
			} else if(index >= SIPrefixes.Length)
			{
				index = SIPrefixes.Length - 1;
				power = 3 * (SIPrefixes.Length - ZeroPrefixPosition - 1);
			}
			what /= Math.Pow(10, power);
			var unit = SIPrefixes[index];
			var roundTo = Math.Max(3 - digits, 0);
			what = Math.Round(what, roundTo);
			return prefix + what + unit;
		}

		private static readonly string[] SIPrefixes = {
			"p",
			"n",
			"µ",
			"m",
			"",
			"k",
			"M",
			"G",
			"T"
		};

		private const int ZeroPrefixPosition = 4;
	}
}

