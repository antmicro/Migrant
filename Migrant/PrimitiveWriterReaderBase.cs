// *******************************************************************
//
//  Copyright (c) 2012-2015, Antmicro Ltd <antmicro.com>
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
using System.Diagnostics;
using System.Text;
using System.IO;

namespace Antmicro.Migrant
{
    public abstract class PrimitiveWriterReaderBase
    {
        protected PrimitiveWriterReaderBase(string logPrefix, Stream stream, bool buffered, int bufferSize)
        {
            #if DEBUG
            buffered &= !Serializer.DisableBuffering;
            #endif

            this.stream = stream;
            if(buffered)
            {
                buffer = new byte[bufferSize];
            }
            this.buffered = buffered;

            #if DEBUG
            prefix = logPrefix;
            currentTag = new StringBuilder();
            logFile = Serializer.PrimitiveWriterReaderLogFile;
            #endif
        }

        [Conditional("DEBUG")]
        public void Tag(string field, object arg = null)
        {
            currentTag.Clear();
            currentTag.AppendFormat("{0}: offset = {1,5}, field = {2,-20}, ", prefix, Position, field);
            level = 0;
            this.arg = arg;
        }

        [Conditional("DEBUG")]
        protected void Enter()
        {
            if(logFile == null)
            {
                return;
            }
            level++;
        }

        [Conditional("DEBUG")]
        protected void Leave<T>(T value)
        {
            if(logFile == null)
            {
                return;
            }

            level--;
            if(level > 0 || currentTag.Length == 0)
            {
                return;
            }

            currentTag.AppendFormat("type = {0}, value = {1}", typeof(T).Name, value);
            if(arg != null)
            {
                currentTag.AppendFormat(", arg = {0}", arg);
            }
            File.AppendAllText(logFile, currentTag.AppendLine().ToString());
            currentTag.Clear();
            arg = null;
        }
        
        /// <summary>
        /// Gets the current position.
        /// </summary>
        /// <value>
        /// The position, which is the number of bytes written/read after this object was
        /// constructed.
        /// </value>
        public long Position
        {
            get
            {
                return currentPosition + currentBufferPosition;
            }
        }
        
        protected int currentBufferPosition;
        protected long currentPosition;
        protected readonly byte[] buffer;
        protected readonly Stream stream;
        protected readonly bool buffered;

        #if DEBUG
        private int level;
        private object arg;

        private readonly string logFile;
        private readonly StringBuilder currentTag;
        private readonly string prefix;
        #endif
    }
}

