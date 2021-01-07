//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

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

