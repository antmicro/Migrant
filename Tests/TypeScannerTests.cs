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
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace AntMicro.Migrant.Tests
{
	[TestFixture]
	public class TypeScannerTests
	{
		[Test]
		public void SimpleObjectTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(SimpleA));
			CollectionAssert.Contains(scan.GetTypeArray(), typeof(SimpleA));
			Assert.AreEqual(scan.GetTypeArray().Length, 2);
		}

		[Test]
		public void TwoSimpleObjectsTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(SimpleA));
			scan.Scan(typeof(SimpleB));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(), new[] { typeof(SimpleA), typeof(SimpleB), typeof(object) });
		}

		[Test]
		public void SimpleObjectsWithDuplicatesTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(SimpleA));
			scan.Scan(typeof(SimpleB));
			scan.Scan(typeof(SimpleA));
			scan.Scan(typeof(SimpleB));
			scan.Scan(typeof(SimpleB));
			CollectionAssert.AllItemsAreUnique(scan.GetTypeArray());
		}

		[Test]
		public void SimpleInheritanceTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(CFromA));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(), new[] { typeof(SimpleA), typeof(CFromA), typeof(object) });
		}

		[Test]
		public void DoubleInheritanceTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(DFromC));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(),	new[] { typeof(SimpleA), typeof(CFromA), typeof(DFromC), typeof(object) });
		}

		[Test]
		public void SimpleInclusionTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(EWithA));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(), new[] { typeof(SimpleA), typeof(EWithA), typeof(object) });
		}

		[Test]
		public void InheritanceAndInclusionTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(FFromB));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(),
				new[] { typeof(SimpleA), typeof(DFromC),	typeof(CFromA),	typeof(FFromB),	typeof(SimpleB), typeof(object)	});
		}

		[Test]
		public void SimpleBreakOnIllegalTest()
		{
			try
			{
				var scan = new TypeScanner();
				scan.Scan(typeof(IllegalG));
				Assert.Fail();
			} 
			catch(ArgumentException)
			{

			}
		}

		[Test]
		public void SimpleBreakOnInheritedIllegalTest()
		{
			try
			{
				var scan = new TypeScanner();
				scan.Scan(typeof(HFromG));
				Assert.Fail();
			} 
			catch(ArgumentException)
			{

			}
		}

		[Test]
		public void BreakOnPointer()
		{
			try
			{
				var scan = new TypeScanner();
				scan.Scan(typeof(void*));
				Assert.Fail();
			}
			catch(ArgumentException)
			{

			}
		}

		private class SimpleA
		{
		}

		private class SimpleB
		{
		}

		private class CFromA : SimpleA
		{
		}

		private class DFromC : CFromA
		{
		}

		private class EWithA
		{
#pragma warning disable 0169
			private SimpleA a;
#pragma warning restore
		}

		private class FFromB : SimpleB
		{
#pragma warning disable 0169
			private DFromC b;
#pragma warning restore
		}

		private class IllegalG
		{
			#pragma warning disable 0169
			private IntPtr ptr;
			#pragma warning restore
		}

		private class HFromG:IllegalG
		{
		}

		[Test]
		public void SimpleArrayListTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(ArrayList));
			CollectionAssert.Contains(scan.GetTypeArray(), typeof(ArrayList));
			Assert.AreEqual(scan.GetTypeArray().Length, 2);
		}

		[Test]
		public void SimpleHashtableTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(Hashtable));
			CollectionAssert.Contains(scan.GetTypeArray(), typeof(Hashtable));
			Assert.AreEqual(scan.GetTypeArray().Length, 2);
		}

		[Test]
		public void SimpleListTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(List<CFromA>));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(),
			                               new[] {	typeof(SimpleA), typeof(CFromA), typeof(object), typeof(List<CFromA>) });
		}

		[Test]
		public void SimpleDictionaryTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(Dictionary<CFromA, EWithA>));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(),
			                               new[] {
				typeof(SimpleA),
				typeof(CFromA),
				typeof(EWithA),
				typeof(object),
				typeof(Dictionary<CFromA, EWithA>)
			}
			);

		}

		[Test]
		public void SimpleIDictionaryTest()
		{
			var scan = new TypeScanner();
			scan.Scan(typeof(IDictionary<CFromA, EWithA>));
			CollectionAssert.AreEquivalent(scan.GetTypeArray(),
			                               new[] { typeof(SimpleA), typeof(CFromA), typeof(EWithA), typeof(object) });
		}

		[Test]
		public void IllegalDictionaryTest()
		{
			try
			{
				var scan = new TypeScanner();
				scan.Scan(typeof(Dictionary<String,HFromG>));
				Assert.Fail();
			} 
			catch(ArgumentException)
			{

			}
		}

		[Test]
		public void DelegateDest()
		{
			try
			{
				var scan = new TypeScanner();
				scan.Scan(typeof(Delegate));
				Assert.Fail();
			} 
			catch(ArgumentException)
			{

			}
		}

		[Test]
		public void ThreadDest()
		{
			try
			{
				var scan = new TypeScanner();
				scan.Scan(typeof(Thread));
				Assert.Fail();
			} 
			catch(ArgumentException)
			{

			}
		}
	}
}

