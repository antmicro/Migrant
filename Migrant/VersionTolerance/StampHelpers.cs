//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Antmicro.Migrant.VersionTolerance
{
    internal static class StampHelpers
    {
        public static bool IsStampNeeded(TypeDescriptor type, bool treatCollectionAsUserObject)
        {
            return !Helpers.IsWriteableByPrimitiveWriter(type.UnderlyingType) && (!CollectionMetaToken.IsCollection(type) || treatCollectionAsUserObject);
        }

        public static IEnumerable<FieldInfo> GetFieldsInSerializationOrder(Type type, bool withTransient = false)
        {
            return type.GetAllFields(true).Where(f => withTransient || !f.IsTransient()).OrderBy(f => f.Name);
        }
    }
}

