/*
  Copyright (c) 2014 Antmicro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)
   * Mateusz Holenko (mholenko@antmicro.com)

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
                    structArray[i].B = 1.0 / i;
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

