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

namespace Migrantoid
{
    /// <summary>
    /// When this attribute is placed on a field, such field is filled with
    /// the newly constructed object of a same type as field. To construct
    /// such object, the parameters given in a attribute are used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ConstructorAttribute : TransientAttribute
    {
        /// <summary>
        /// Initializes attribute with given parameters which will be later passed to the
        /// constructor.
        /// </summary>
        /// <param name='parameters'>
        /// Parameters.
        /// </param>
        public ConstructorAttribute(params object[] parameters)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// Parameters passed to the attribute.
        /// </summary>
        public object[] Parameters { get; private set; }

        
    }
}

