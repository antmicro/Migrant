/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)

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
using System.Linq;
using CommandLine;
using AntMicro.Migrant.PerformanceTests;
using System.Collections.Generic;

namespace AntMicro.Migrant.ResultBrowser
{
	public class MainClass
	{
		public static int Main(string[] args)
		{
			var options = new Options();
			if(!new CommandLineParser(new CommandLineParserSettings(Console.Out)).ParseArguments(args, options))
			{
				Console.Error.WriteLine("Wrong options.");
				return 1;
			}
			var resultsDb = new ResultsDb(BaseFixture.DbFileName);
			IEnumerable<Result> results = resultsDb.GetAllResults().OrderBy(x => x.Date);
			if(options.Contains != null)
			{
				results = results.Where(x => x.Name.Contains(options.Contains));
			}
			foreach(var result in results)
			{
				Console.Out.WriteLine(string.Format("{0:dd-MM-yy HH:mm} {1} {2} {3:0.000}", result.Date, result.Name.PadRight(60), 
				                                    (result.Average.NormalizeDecimal() + 's').PadRight(9), result.StandardDeviation/result.Average));
			}

			return 0;
		}
	}
}
