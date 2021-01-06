/*
  Copyright (c) 2021, Konrad Kruczyński

  Authors:
   * Konrad Kruczyński (konrad.kruczynski@gmail.com)

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
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Migrantoid.Tests
{
    [TestFixture(false, false, true)]
    [TestFixture(true, false, true)]
    [TestFixture(false, true, true)]
    [TestFixture(true, true, true)]
    [TestFixture(false, false, false)]
    [TestFixture(true, false, false)]
    [TestFixture(false, true, false)]
    [TestFixture(true, true, false)]
    public sealed class RecipeTests : BaseTestWithSettings
    {
        public RecipeTests(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useTypeStamping)
            : base(useGeneratedSerializer, useGeneratedDeserializer, false, false, useTypeStamping, false)
        {

        }

        [Test]
        public void ShouldSerializeRegex()
        {
            var regex = new Regex("[0-9]");
            var copy = SerializerClone(regex);
            Assert.AreEqual(regex.ToString(), copy.ToString());
        }

        [Test]
        public void ShouldSerializeCompiledRegex()
        {
            var regex = new Regex(@"\d+", RegexOptions.Compiled);
            var copy = SerializerClone(regex);
            Assert.AreEqual(regex.ToString(), copy.ToString());
            Assert.AreEqual(regex.Options, copy.Options);
        }

        [Test]
        public void ShouldSerializeRegexWithGivenMatchTimeout()
        {
            var regex = new Regex(@"\d+", default(RegexOptions), TimeSpan.FromMinutes(1));
            var copy = SerializerClone(regex);
            Assert.AreEqual(regex.ToString(), copy.ToString());
            Assert.AreEqual(regex.MatchTimeout, copy.MatchTimeout);
        }
    }
}
