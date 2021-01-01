﻿/*
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
using System.Linq;
using System.Diagnostics;
using Migrantoid.PerformanceTester.Tests;
using Migrantoid.PerformanceTester.Serializers;
using Migrantoid;
using System.Collections.Generic;

namespace Migrantoid.PerformanceTester
{
    public class TestRunner
    {
        public TestRunner(TestType testType, SerializerType serializerType)
        {
            this.testType = testType;
            serializer = SerializerFactory.Produce(serializerType);
        }

        public TestResult Run<T>(ITest<T> test)
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

        private static TestResult Run(Action whatToRun, Action before = null, Action after = null)
        {
            before = before ?? new Action(() => {});
            after = after ?? new Action(() => {});

            PrintProgress("warming up...");
            for(var i = 0; i < WarmUpRounds; i++)
            {
                before();
                whatToRun();
                after();
            }

            var rounds = 0;
            var results = new List<double>();
            while(true)
            {
                before();
                results.Add(Measure(whatToRun));
                after();
                rounds++;
                if(rounds < MinimalNumberOfRounds)
                {
                    PrintProgress(string.Format("Round {0}.", rounds));
                }
                else
                {
                    var average = results.Skip(results.Count - MinimalNumberOfRounds).Average();
                    var deviation = results.Skip(results.Count - MinimalNumberOfRounds).StandardDeviation();
                    var percentDeviation = deviation/average;
                    PrintProgress(string.Format("Round {1}, {0:#0.#}% deviation.", percentDeviation*100.0, rounds));
                    if(percentDeviation < DesiredDeviation)
                    {
                        PrintProgress("done.");
                        Console.WriteLine();
                        return new TestResult(average, deviation);
                    }
                }
            }
        }

        private static double Measure(Action whatToRun)
        {
            var stopwatch = Stopwatch.StartNew();
            whatToRun();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalSeconds;
        }

        private static void PrintProgress(string what)
        {
            Console.CursorLeft = 0;
            Console.Write(Enumerable.Repeat(" ", Console.BufferWidth - 1).Aggregate((x, y) => x + y));
            Console.CursorLeft = 0;
            Console.Write("Running... {0}", what);
        }

        private readonly TestType testType;
        private readonly ISerializer serializer;

        private const int WarmUpRounds = 2;
        private const int MinimalNumberOfRounds = 7;
        private const double DesiredDeviation = 0.05;
    }
}

