//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.PerformanceTester.Tests
{
    public class SimpleClassTest : ITest<SimpleClassTest.SimpleClass[]>
    {
        public SimpleClass[] Object
        {
            get
            {
                var classArray = new SimpleClass[900000];
                for(var i = 0; i < classArray.Length; i++)
                {
                    var simpleClass = new SimpleClass { A = i, B = 1.0 / (i + 1) };
                    classArray[i] = simpleClass;
                }
                return classArray;
            }
        }

        [ProtoBuf.ProtoContract]
        public class SimpleClass
        {
            [ProtoBuf.ProtoMember(1)]
            public int
                A;
            [ProtoBuf.ProtoMember(2)]
            public double
            B;
        }
    }
}

