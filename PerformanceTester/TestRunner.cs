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
using System.IO;
using System.Diagnostics;
using Antmicro.Migrant.PerformanceTester.Tests;
using Antmicro.Migrant.PerformanceTester.Serializers;

namespace Antmicro.Migrant.PerformanceTester
{
    public class TestRunner
    {
        public TestRunner(TestType testType, SerializerType serializerType)
        {
            this.testType = testType;
            serializer = SerializerFactory.Produce(serializerType);
        }

        public double Run<T>(ITest<T> test)
        {
            var stream = new MemoryStream();
            if(testType == TestType.Serialization)
            {
                return Run(
                    () => serializer.Serialize(test.Object, stream),
                    after: () => stream.Seek(0, SeekOrigin.Begin));
            }
            else
            {
                serializer.Serialize(test.Object, stream);
                return Run(() =>
                    serializer.Deserialize<T>(stream),
                    before: () => stream.Seek(0, SeekOrigin.Begin));
            }
        }

        private static double Run(Action whatToRun, Action before = null, Action after = null)
        {
            before = before ?? new Action(() => {});
            after = after ?? new Action(() => {});

            for(var i = 0; i < WarmUpRounds; i++)
            {
                before();
                whatToRun();
                after();
            }

            before();
            var result = Measure(whatToRun);
            after();

            return result;
        }

        private static double Measure(Action whatToRun)
        {
            var stopwatch = Stopwatch.StartNew();
            whatToRun();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalSeconds;
        }

        private const int WarmUpRounds = 5;

        private readonly TestType testType;
        private readonly ISerializer serializer;
    }
}

