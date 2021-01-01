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
using Migrantoid.PerformanceTester.Tests;
using CommandLine;
using System.IO;

namespace Migrantoid.PerformanceTester
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var options = new Options();
            if(!Parser.Default.ParseArguments(args, options))
            {
                Console.Error.WriteLine("Unsupported options provided.");
                return;
            }
            if(options.Id == null)
            {
                options.Id = typeof(Serializer).Assembly.GetName().Version.ToString();
            }

            Console.WriteLine("==========================");
            Console.WriteLine("Migrant performance tester");
            Console.WriteLine("==========================");
            Console.WriteLine("");

            foreach(SerializerType serializerType in Enum.GetValues(typeof(SerializerType)))
            {
                Console.WriteLine("----------------");
                Console.WriteLine("Serializer type: {0}", serializerType);
                Console.WriteLine("----------------");
                Console.WriteLine("");

                foreach(TestType testType in Enum.GetValues(typeof(TestType)))
                {
                    Console.WriteLine("++++++++++");
                    Console.WriteLine("Test type: {0}", testType);
                    Console.WriteLine("++++++++++");
                    Console.WriteLine("");

                    foreach(var test in new ITest<object>[] { new SimpleStructTest(), new SimpleClassTest() })
                    {
                        var runner = new TestRunner(testType, serializerType);
                        var result = runner.Run((dynamic)test);
                        Console.WriteLine("{0,20}: {1}", test.GetType().Name, result);
                        if(options.OutputFile != null)
                        {
                            File.AppendAllText(options.OutputFile, string.Format("{0}-{1}-{2}-{3};{4};{5}{6}", serializerType, test.GetType().Name, testType, options.Id, result.Average, result.StandardDeviation, Environment.NewLine));
                        }
                    }
                    Console.WriteLine("");
                }
            } 
        }
    }
}
