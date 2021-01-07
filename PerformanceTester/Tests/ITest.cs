//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

namespace Antmicro.Migrant.PerformanceTester.Tests
{
    public interface ITest<out T>
    {
        T Object { get; }
    }
}

