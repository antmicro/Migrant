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
				// TODO: some kind of help
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
