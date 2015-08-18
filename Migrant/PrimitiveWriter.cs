/*
  Copyright (c) 2012-2015 Antmicro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)
   * Mateusz Holenko (mholenko@antmicro.com)

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
using System.IO;
using System.Text;

namespace Antmicro.Migrant
{
    /// <summary>
    /// Provides the mechanism for writing primitive values into a stream.
    /// </summary>
    /// <remarks>
    /// Can be used as a replacement for the <see cref="System.IO.BinaryWriter" /> . Provides
    /// more compact output and reads no more data from the stream than requested. Although
    /// the underlying format is not specified at this point, it is guaranteed to be consistent with
    /// <see cref="Antmicro.Migrant.PrimitiveReader" />. Writer has to be disposed after used,
    /// otherwise stream position corruption and data loss can occur. Writer does not possess the
    /// stream and does not close it after dispose.
    /// </remarks>
    public sealed class PrimitiveWriter : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.PrimitiveWriter" /> class.
        /// </summary>
        /// <param name='stream'>
        /// The underlying stream which will be used to write data. Has to be writeable.
        /// </param>
        /// <param name='buffered'>
        /// True if writes should be buffered, false when they should be immediately passed to
        /// the stream. With false also no final padding is used. Note that corresponding
        /// PrimitiveReader has to use the same value for this parameter.
        /// </param>
        public PrimitiveWriter(Stream stream, bool buffered = true)
        {
            this.stream = stream;
            #if DEBUG
            buffered &= !Serializer.DisableBuffering;
            #endif
            if(buffered)
            {
                buffer = new byte[BufferSize];
            }
            this.buffered = buffered;
        }

        /// <summary>
        /// Gets the current position.
        /// </summary>
        /// <value>
        /// The position, which is the number of bytes written after this object was
        /// constructed.
        /// </value>
        public long Position
        {
            get
            {
                return currentPosition + currentBufferPosition;
            }
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Double" />.
        /// </summary>
        public void Write(double value)
        {
            Write(BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Single" />.
        /// </summary>
        public void Write(float value)
        {
            Write(BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.DateTime" />.
        /// </summary>
        public void Write(DateTime value)
        {
            Write(value - Helpers.DateTimeEpoch);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.TimeSpan" />.
        /// </summary>
        public void Write(TimeSpan value)
        {
            // first unit, then value in this unit
            if(value.Ticks % TimeSpan.TicksPerSecond != 0)
            {
                Write((byte)Helpers.TickIndicator);
                Write(value.Ticks);
                return;
            }
            var type = (byte)(value.Hours);
            Write(type);
            Write((ushort)(value.Seconds + 60 * value.Minutes));
            Write(value.Days);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Byte" />.
        /// </summary>
        public void Write(byte value)
        {
            if(buffered)
            {
                CheckBuffer(1);
                buffer[currentBufferPosition++] = value;
            }
            else
            {
                currentBufferPosition++;
                stream.WriteByte(value);
            }
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.SByte" />.
        /// </summary>
        public void Write(sbyte value)
        {
            Write((byte)value);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Int16" />.
        /// </summary>
        public void Write(short value)
        {
#if DEBUG
            if(Serializer.DisableVarints)
            {
                InnerWriteInteger((ushort)value, sizeof(short) + 1);
                return;
            }
#endif
            var valueToWrite = (value << 1) ^ (value >> 15);
            InnerWriteInteger((ushort)valueToWrite, sizeof(short) + 1);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.UInt16" />.
        /// </summary>
        public void Write(ushort value)
        {
            InnerWriteInteger(value, sizeof(ushort) + 1);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Int32" />.
        /// </summary>
        public void Write(int value)
        {
#if DEBUG
            if(Serializer.DisableVarints)
            {
                InnerWriteInteger((uint)value, sizeof(int) + 1);
                return;
            }
#endif
            var valueToWrite = (value << 1) ^ (value >> 31);
            InnerWriteInteger((uint)valueToWrite, sizeof(int) + 1);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.UInt32" />.
        /// </summary>
        public void Write(uint value)
        {
            InnerWriteInteger(value, sizeof(uint) + 1);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Int64" />.
        /// </summary>
        public void Write(long value)
        {
#if DEBUG
            if(Serializer.DisableVarints)
            {
                Write((ulong)value);
                return;
            }
#endif
            //zig-zag notation
            var valueToWrite = (value << 1) ^ (value >> 63);
            InnerWriteInteger((ulong)valueToWrite, sizeof(long) + 2);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.UInt64" />.
        /// </summary>
        public void Write(ulong value)
        {
            InnerWriteInteger(value, sizeof(ulong) + 2);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Char" />.
        /// </summary>
        public void Write(char value)
        {
            Write((ushort)value);
        }

        /// <summary>
        /// Writes the specified value of type <see cref="System.Boolean" />.
        /// </summary>
        public void Write(bool value)
        {
            Write((byte)(value ? 1 : 0));
        }


        /// <summary>
        /// Writes the specified value of type <see cref="System.Guid" />.
        /// </summary>
        public void Write(Guid guid)
        {
            InnerChunkWrite(guid.ToByteArray());
        }

        /// <summary>
        /// Writes the specified string.
        /// </summary>
        public void Write(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            Write(bytes.Length);
            InnerChunkWrite(bytes);
        }

        /// <summary>
        /// Writes the specified <see cref="System.Decimal"/> .
        /// </summary>
        public void Write(decimal value)
        {
            var bytes = decimal.GetBits(value);
            Write(bytes[0]);
            Write(bytes[1]);
            Write(bytes[2]);
            Write(bytes[3]);
        }

        /// <summary>
        /// Writes the specified bytes array.
        /// </summary>
        /// <param name='bytes'>
        /// The array which content will be written.
        /// </param>
        public void Write(byte[] bytes)
        {
            InnerChunkWrite(bytes);
        }

        /// <summary>
        /// Write the specified bytes array, starting at offset and writing count from it.
        /// </summary>
        /// <param name="bytes">The array which is a source to write.</param>
        /// <param name="offset">Index of the array to start writing at.</param>
        /// <param name="count">Total bytes to write.</param>
        public void Write(byte[] bytes, int offset, int count)
        {
            InnerChunkWrite(bytes, offset, count);
        }

        /// <summary>
        /// Copies given number of bytes from the source stream to the underlying stream.
        /// </summary>
        /// <param name='source'>
        /// Readable stream, from which data will be copied.
        /// </param>
        /// <param name='howMuch'>
        /// The amount of a data to copy in bytes.
        /// </param>
        public void CopyFrom(Stream source, long howMuch)
        {
            var localBuffer = new byte[Helpers.MaximalPadding];
            Flush();
            int read;
            while((read = source.Read(localBuffer, 0, (int)Math.Min(localBuffer.Length, howMuch))) > 0)
            {
                howMuch -= read;
                currentPosition += read;
                stream.Write(localBuffer, 0, read);
            }
            if(howMuch > 0)
            {
                throw new EndOfStreamException(string.Format("End of stream reached while {0} more bytes expected.", howMuch));
            }
        }

        /// <summary>
        /// Flushes the buffer and pads the stream with sufficient amount of data to be compatible 
        /// with the <see cref="Antmicro.Migrant.PrimitiveReader" />. It is not necessary to call this method
        /// when buffering is not used.
        /// </summary>
        /// <remarks>
        /// Call <see cref="Dispose"/> when you are finished using the <see cref="Antmicro.Migrant.PrimitiveWriter"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Antmicro.Migrant.PrimitiveWriter"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the
        /// <see cref="Antmicro.Migrant.PrimitiveWriter"/> so the garbage collector can reclaim the memory that the
        /// <see cref="Antmicro.Migrant.PrimitiveWriter"/> was occupying.
        /// </remarks>
        public void Dispose()
        {
            Flush();
            Pad();
        }

        private void InnerWriteInteger(ulong value, int sizeInBytes)
        {
            byte valueToWrite;
#if DEBUG
            if(Serializer.DisableVarints)
            {
                if(buffered)
                {
                    CheckBuffer(sizeof(ulong));
                }
                ulong current = 0;
                int bitsShift = (sizeof(ulong) - 1) * 8;
                ulong mask = ((ulong)0xFF << bitsShift);
                for(int i = 0; i < sizeof(ulong); ++i)
                {
                    current = (value & mask) >> bitsShift;
                    valueToWrite = (byte)current;
                    if(buffered)
                    {
                        buffer[currentBufferPosition + i] = valueToWrite;
                    }
                    else
                    {
                        stream.WriteByte(valueToWrite);
                    }
                    mask >>= 8;
                    bitsShift -= 8;
                }

                if(buffered)
                {
                    currentBufferPosition += sizeof(ulong);
                }
                else
                {
                    currentPosition += sizeof(ulong);
                }
                return;
            }
#endif
            if(buffered)
            {
                CheckBuffer(sizeInBytes);
            }
            while(value > 127)
            {
                valueToWrite = (byte)(value | 128);
                if(buffered)
                {
                    buffer[currentBufferPosition++] = valueToWrite;
                }
                else
                {
                    currentPosition++;
                    stream.WriteByte(valueToWrite);
                }
                value >>= 7;
            }
            valueToWrite = (byte)(value & 127);
            if(buffered)
            {
                buffer[currentBufferPosition++] = valueToWrite;
            }
            else
            {
                currentPosition++;
                stream.WriteByte(valueToWrite);
            }
        }

        private void InnerChunkWrite(byte[] data)
        {
            InnerChunkWrite(data, 0, data.Length);
        }

        private void InnerChunkWrite(byte[] data, int offset, int length)
        {
            if(buffered)
            {
                CheckBuffer(length);
            }
            else
            {
                stream.Write(data, offset, length);
                currentPosition += length;
                return;
            }
            if(length > BufferSize)
            {
                stream.Write(data, offset, length);
                currentPosition += length;
            }
            else
            {
                Array.Copy(data, offset, buffer, currentBufferPosition, length);
                currentBufferPosition += length;
            }
        }

        private void CheckBuffer(int maxBytesToWrite)
        {
            if(buffer.Length - currentBufferPosition >= maxBytesToWrite)
            {
                return;
            }
            // we need to flush the buffer
            Flush();
        }

        private void Flush()
        {
            if(!buffered)
            {
                return;
            }           
            stream.Write(buffer, 0, currentBufferPosition);
            currentPosition += currentBufferPosition;
            currentBufferPosition = 0;
        }

        private void Pad()
        {
            if(!buffered)
            {
                return;
            }
            var bytesToPad = Helpers.GetCurrentPaddingValue(currentPosition);
            var pad = new byte[bytesToPad];
            stream.Write(pad, 0, pad.Length);
        }

        private readonly byte[] buffer;
        private int currentBufferPosition;
        private long currentPosition;
        private readonly Stream stream;
        private readonly bool buffered;
        private const int BufferSize = 4 * 1024;
    }
}

