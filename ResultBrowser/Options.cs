using System;
using CommandLine;

namespace AntMicro.Migrant.ResultBrowser
{
	internal class Options
	{
		[Option("c", "contains", HelpText = "Filter tests containg given string.")]
		public string Contains { get; set; }
	}
}

