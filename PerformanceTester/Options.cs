//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using CommandLine;

namespace Antmicro.Migrant.PerformanceTester
{
    public sealed class Options
    {
        [Option('o', "output", HelpText = "File to write CSV results to.")]
        public string OutputFile { get; set; }

        [Option('i', "id", HelpText = "Id to use in the CSV results file.")]
        public string Id { get; set; }
    }
}

