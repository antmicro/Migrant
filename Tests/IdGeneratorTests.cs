//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using NUnit.Framework;
using System.Collections.Generic;

namespace Antmicro.Migrant.Tests
{
    [TestFixture]
    public class IdGeneratorTests
    {
        [Test]
        public void ShouldGenerateUniqueId()
        {
            const int ObjectsNo = 100000;
            var identifier = new ObjectIdentifier();
            var objects = new object[ObjectsNo];
            for(var i = 0; i < objects.Length; i++)
            {
                objects[i] = new object();
            }
            var generated = new HashSet<int>();
            foreach(var obj in objects)
            {
                bool fake;
                var id = identifier.GetId(obj, out fake);
                Assert.IsTrue(generated.Add(id), "Not unique ID was generated.");
            }
        }
    }
}

