//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.PerformanceTester.Tests
{
    public class SimpleStructTest : ITest<SimpleStructTest.SimpleStruct[]>
    {
        public SimpleStruct[] Object
        {
            get
            {
                var structArray = new SimpleStruct[900000];
                for(var i = 0; i < structArray.Length; i++)
                {
                    structArray[i].A = i;
                    structArray[i].B = 1.0 / (i + 1);
                }
                return structArray;
            }
        }
       
        [ProtoBuf.ProtoContract]
        public struct SimpleStruct
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

