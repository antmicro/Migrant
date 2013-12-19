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
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Antmicro.Migrant.ResultBrowser
{
    internal class Options : CommandLineOptionsBase
    {
        [Option("c", "contains", HelpText = "Filter tests containg given string.")]
        public string Contains { get; set; }

        [Option("f", "fileName", HelpText = "Tests database.", Required = true)]
        public string FileName { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText 
            {
                Heading = new HeadingInfo("ResultBrowser"),
                Copyright = new CopyrightInfo("Antmicro Ltd", 2013),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: ResultBrowser [-c string] -f fileName");
            help.AddOptions(this);
            return help;
        }
    }
}

