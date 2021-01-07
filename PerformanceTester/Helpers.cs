//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Migrant.PerformanceTester
{
    internal static class Helpers
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

