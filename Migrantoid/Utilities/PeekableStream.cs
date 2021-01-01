// *******************************************************************
//
//  Copyright (c) 2012-2014, Antmicro Ltd <antmicro.com>
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
using System.IO;

namespace Antmicro.Migrant.Utilities
{
    internal class PeekableStream : Stream
    {
        public PeekableStream(Stream underlyingStream)
        {
            this.underlyingStream = underlyingStream;
        }

        public override int ReadByte()
        {
            if(waitingByte.HasValue)
            {
                var value = waitingByte.Value;
                waitingByte = null;
                return value;
            }
            return underlyingStream.ReadByte();
        }

        public int Peek()
        {
            if(waitingByte != null)
            {
                throw new InvalidOperationException("Peek cannot be executed more than one in a row.");
            }
            var value = underlyingStream.ReadByte();
            if(value == -1)
            {
                return -1;
            }
            waitingByte = (byte)value;
            return waitingByte.Value;
        }

        public override bool CanRead
        {
            get
            {
                return underlyingStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return underlyingStream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return underlyingStream.CanWrite;
            }
        }

        public override void Flush()
        {
            underlyingStream.Flush();
        }

        public override long Position
        {
            get
            {
                return underlyingStream.Position;
            }
            set
            {
                underlyingStream.Position = value;
            }
        }

        public override long Length
        {
            get
            {
                return underlyingStream.Length;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return underlyingStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return underlyingStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            underlyingStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            underlyingStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            underlyingStream.WriteByte(value);
        }

        private byte? waitingByte;
        private readonly Stream underlyingStream;
    }
}

