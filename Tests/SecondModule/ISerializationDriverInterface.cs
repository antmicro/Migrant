//
// Copyright (c) 2012-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Tests.SecondModule
{
    public interface ISerializationDriverInterface
    {
        void RunAction(Action<Func<string, Type>> action);
    }
}
