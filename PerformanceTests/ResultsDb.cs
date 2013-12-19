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
using System.Collections.Generic;
using System.IO;

namespace Antmicro.Migrant.PerformanceTests
{
	public sealed class ResultsDb
	{
		public ResultsDb(string fileName)
		{
			this.fileName = fileName;
			if(!File.Exists(fileName))
			{
				Clear();
			}
		}

		public void Append(Result result)
		{
			using(var writer = new BinaryWriter(new FileStream(fileName, FileMode.Append)))
			{
				writer.Write(result.Name);
				writer.Write(result.Average);
				writer.Write(result.StandardDeviation);
				writer.Write(result.Date.Ticks);
			}
		}

		public IEnumerable<Result> GetAllResults()
		{
			var results = new List<Result>();
			using(var reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
			{
				var magic = reader.ReadInt32();
				if(magic != Magic)
				{
					throw new InvalidOperationException(string.Format("Invalid db file, wrong magic (0x{0:X}, should be 0x{1:X}).", magic, Magic));
				}
				var version = reader.ReadInt32();
				if(version != CurrentVersion)
				{
					throw new InvalidOperationException(string.Format("Invalid db file, wrong version {0}, should be {1}.", version, CurrentVersion));
				}
				while(reader.PeekChar() != -1)
				{
					var testName = reader.ReadString();
					var average = reader.ReadDouble();
					var standardDeviation = reader.ReadDouble();
					var date = new DateTime(reader.ReadInt64());
					results.Add(new Result(testName, average, standardDeviation, date));
				}
			}
			return results;
		}

		public void Clear()
		{
			using(var writer = new BinaryWriter(File.Create(fileName)))
			{
				writer.Write(Magic);
				writer.Write(CurrentVersion);
			}
		}

		private readonly string fileName;

		private const int Magic = 0x12FFE567;
		private const int CurrentVersion = 1;
	}
}

