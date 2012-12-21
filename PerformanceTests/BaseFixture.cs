/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

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
using System.Diagnostics;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace AntMicro.Migrant.PerformanceTests
{
	public abstract class BaseFixture
	{
		[TestFixtureSetUp]
		public void InitFixture()
		{
			resultsDb = new ResultsDb(DbFileName);
		}

		public const string DbFileName = "testResults.dat";

		protected void Run(Action whatToRun, string testNameSuffix = "", Action before = null, Action after = null)
		{
			results = new List<double>();
			before = before ?? new Action(() => {});
			after = after ?? new Action(() => {});
			for(var i = 0; i < WarmUpRounds; i++)
			{
				before();
				whatToRun();
				after();
			}

			while(true)
			{
				before();
				Measure(whatToRun);
				after();

				if(results.Count >= MinimalNumberOfRuns)
				{
					var currentAverage = results.Average();
					var currentStdDev = results.StandardDeviation();
					results.RemoveAll(x => Math.Abs(x - currentAverage) > 3*currentStdDev);
					currentStdDev = results.StandardDeviation();
					currentAverage = results.Average();
					if(currentStdDev/currentAverage <= MinimalRequiredStandardDeviation)
					{
						break;
					}
				}
				if(results.Count > MaximalNumberOfRuns)
				{
					Assert.Fail(string.Format("Maximal number of runs {0} was exceeded, with standard deviation {1:0.00} > {2:0.00}.", MaximalNumberOfRuns, 
					                          results.StandardDeviation()/results.Average(), MinimalRequiredStandardDeviation));
				}
			}
			var average = results.Average();
			var stdDev = results.StandardDeviation();
			var testName = string.Format("{0}:{1}", TestContext.CurrentContext.Test.Name, testNameSuffix);
			resultsDb.Append(new Result(testName, average, stdDev, SetUp.TestDate));
			File.AppendAllLines(SetUp.TestLogFileName, new [] { string.Format("{0}\t{1}s\t{2:0.00}", testName, average.NormalizeDecimal(), stdDev/average) } );
		}

		private void Measure(Action whatToRun)
		{
			var stopwatch = Stopwatch.StartNew();
			whatToRun();
			stopwatch.Stop();
			results.Add(stopwatch.Elapsed.TotalSeconds);
		}

		private List<double> results;
		private ResultsDb resultsDb;

		private const int MinimalNumberOfRuns = 15;
		private const double MinimalRequiredStandardDeviation = 0.1;
		private const double OutlierPercentage = 0.1;
		private const int MaximalNumberOfRuns = 500;
		private const int WarmUpRounds = 3;
	}
}

