// *******************************************************************
//
//  Copyright (c) 2012-2016, Antmicro Ltd <antmicro.com>
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Migrant.BultinSurrogates
{
    internal class SurrogateForReadOnlyCollection
    {
        public SurrogateForReadOnlyCollection(IEnumerable readOnlyCollection)
        {
            // here we test if the object is in fact a ReadOnlyCollection<> collection
            var type = readOnlyCollection.GetType();
            if(!(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReadOnlyCollection<>)))
            {
                throw new Exception("It looks like you didn't pass `ReadOnlyCollection` object at all!");
            }
            typeAQN = type.AssemblyQualifiedName;

            content = new List<object>();
            foreach(var element in readOnlyCollection)
            {
                content.Add(element);
            }
        }

        public object Restore()
        {
            var type = Type.GetType(typeAQN);
            return Activator.CreateInstance(type, content);
        }

        private List<object> content;
        private string typeAQN;
    }
}

