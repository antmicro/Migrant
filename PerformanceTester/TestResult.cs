//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.PerformanceTester
{
    public struct TestResult
    {
        public TestResult(double average, double standardDeviation) : this()
        {
            Average = average;
            StandardDeviation = standardDeviation;
        }

        public override string ToString()
        {
            return string.Format("{0}s (±{1:#0.#}%)", Average.NormalizeDecimal(), StandardDeviation / Average * 100.0);
        }

        public double Average { get; private set; }
        public double StandardDeviation { get; private set; }
    }
}

