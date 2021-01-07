//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using Antmicro.Migrant.PerformanceTester.Tests;
using CommandLine;
using System.IO;

namespace Antmicro.Migrant.PerformanceTester
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
