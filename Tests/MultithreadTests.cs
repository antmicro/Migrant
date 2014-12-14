// *******************************************************************
//
//  Copyright (c) 2011-2014, Antmicro Ltd <antmicro.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// *******************************************************************
using System;
using NUnit.Framework;
using System.Threading.Tasks;
using System.IO;

namespace Antmicro.Migrant.Tests
{
    [TestFixture]
    public class MultiThreadTests
    {
        [Test]
        public void ShouldProperlySerializeAccesedFromManyThreads()
        {
            const int numberOfTasks = 5;
            const int numberOfObjects = 10000;

            var serializer = new Serializer();
           
            var tasks = new Task[numberOfTasks];
            for(var i = 0; i < numberOfTasks; i++)
            {
                tasks[i] = new Task(() =>
                {
                    var pocos = new SimplePoco[numberOfObjects];
                    for(var j = 0; j < pocos.Length; j++)
                    {
                        pocos[j] = new SimplePoco();
                    }
                    var ms = new MemoryStream();
                    serializer.Serialize(pocos, ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    var copy = serializer.Deserialize<SimplePoco[]>(ms);
                    CollectionAssert.AreEqual(pocos, copy);
                });
            }
            foreach(var task in tasks)
            {
                task.Start();
            }
            Task.WaitAll(tasks);
        }
    }

    internal class SimplePoco
    {
        public SimplePoco()
        {
            I = Helpers.Random.Next(30);
            S = Helpers.GetRandomString(I);
        }

        public string S { get; set; }
        public int I { get; set; }

        public override bool Equals(object obj)
        {
            if(obj == null)
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            if(obj.GetType() != typeof(SimplePoco))
                return false;
            var other = (SimplePoco)obj;
            return S == other.S && I == other.I;
        }
        

        public override int GetHashCode()
        {
            unchecked
            {
                return (S != null ? S.GetHashCode() : 0) ^ I.GetHashCode();
            }
        }        
    }
}

