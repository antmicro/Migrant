//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Utilities
{
    internal interface IIdentifiedElement
    {
        void Read(ObjectReader reader);
        void Write(ObjectWriter writer);
    }
}

