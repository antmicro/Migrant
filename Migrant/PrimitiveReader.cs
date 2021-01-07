//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.IO;
using System.Text;

namespace Antmicro.Migrant
{
    /// <summary>
    /// Provides the mechanism for reading primitive values from a stream.
    /// </summary>
    /// <remarks>
    /// Can be used as a replacement for the <see cref="System.IO.BinaryReader" /> . Provides
    /// more compact output and reads no more data from the stream than requested. Although
    /// the underlying format is not specified at this point, it is guaranteed to be consistent with
    /// <see cref="Antmicro.Migrant.PrimitiveWriter" />. Reader has to be disposed after used,
    /// otherwise stream position corruption can occur. Reader does not possess the stream
    /// and does not close it after dispose.
    /// </remarks>
    public sealed class PrimitiveReader : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.PrimitiveReader" /> class.
        /// </summary>
        /// <param name='stream'>
        /// The underlying stream which will be used to read data. Has to be readable.
        /// </param>
        /// <param name='buffered'> 
        /// True if reads should assume that corresponding PrimitiveWriter used buffering. False otherwise,
        /// then no read prefetching or padding is used. Note that corresponding PrimitiveWriter always have
        /// to have the same value for this parameter.
        /// </param>
        public PrimitiveReader(Stream stream, bool buffered = true)
        {
            this.stream = stream;
            #if DEBUG_FORMAT
            buffered &= !Serializer.DisableBuffering;
            #endif
            if(buffered)
            {
                // buffer size is the size of the maximal padding
                buffer = new byte[Helpers.MaximalPadding];
            }
            this.buffered = buffered;
        }

        /// <summary>
        /// Gets the current position.
        /// </summary>
        /// <value>
        /// The position, which is the number of bytes read after this object was
        /// constructed.
        /// </value>
        public long Position
        {
            get
            {
                return currentPosition - currentBufferSize + currentBufferPosition;
            }
        }

        /// <summary>
        /// Gets current buffering configuration.
        /// </summary>
        /// <value><c>true</c> if this the data read from stream is buffered; otherwise, <c>false</c>.</value>
        public bool IsBuffered
        {
            get
            {
                return buffered;
            }
        }

        /// <summary>
        /// Reads and returns <see cref="System.Double" />.
        /// </summary>
        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        /// <summary>
        /// Reads and returns <see cref="System.Single" />.
        /// </summary>
        public float ReadSingle()
        {
            return (float)BitConverter.Int64BitsToDouble(ReadInt64());
        }

        /// <summary>
        /// Reads and returns <see cref="System.DateTime" />.
        /// </summary>
        public DateTime ReadDateTime()
        {
            return Helpers.DateTimeEpoch + ReadTimeSpan();
        }

        /// <summary>
        /// Reads and returns <see cref="System.TimeSpan" />.
        /// </summary>
        public TimeSpan ReadTimeSpan()
        {
            var type = ReadByte();
            switch(type)
            {
            case Helpers.TickIndicator:
                return TimeSpan.FromTicks(ReadInt64());
            }
            var tms = ReadUInt16();
            var days = ReadInt32();
            return new TimeSpan(days, type, tms / 60, tms % 60);
        }

        /// <summary>
        /// Reads and returns <see cref="System.Byte" />.
        /// </summary>
        public byte ReadByte()
        {
            if(buffered)
            {
                CheckBuffer();
                return buffer[currentBufferPosition++];
            }
            var result = stream.ReadByteOrThrow();

            currentBufferPosition++;
            return result;
        }

        /// <summary>
        /// Reads and returns <see cref="System.SByte" />.
        /// </summary>
        public sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        /// <summary>
        /// Reads and returns <see cref="System.Int16" />.
        /// </summary>
        public short ReadInt16()
        {
#if DEBUG_FORMAT
            if(Serializer.DisableVarints)
            {
                return (short)InnerReadInteger();
            }
#endif
            var value = (short)InnerReadInteger();
            return (short)(((value >> 1) & AllButMostSignificantShort) ^ -(value & 1));
        }

        /// <summary>
        /// Reads and returns <see cref="System.UInt16" />.
        /// </summary>
        public ushort ReadUInt16()
        {
            return (ushort)InnerReadInteger();
        }

        /// <summary>
        /// Reads and returns <see cref="System.Int32" />.
        /// </summary>
        public int ReadInt32()
        {
#if DEBUG_FORMAT
            if(Serializer.DisableVarints)
            {
                return (int)InnerReadInteger();
            }
#endif
            var value = (int)InnerReadInteger();
            return ((value >> 1) & AllButMostSignificantInt) ^ -(value & 1);
        }

        /// <summary>
        /// Reads and returns <see cref="System.UInt32" />.
        /// </summary>
        public uint ReadUInt32()
        {
            return (uint)InnerReadInteger();
        }

        /// <summary>
        /// Reads and returns <see cref="System.Int64" />.
        /// </summary>
        public long ReadInt64()
        {
#if DEBUG_FORMAT
            if(Serializer.DisableVarints)
            {
                return (long)InnerReadInteger();
            }
#endif
            var value = (long)InnerReadInteger();
            return ((value >> 1) & AllButMostSignificantLong) ^ -(value & 1);
        }

        /// <summary>
        /// Reads and returns <see cref="System.UInt64" />.
        /// </summary>
        public ulong ReadUInt64()
        {
            return InnerReadInteger();
        }

        /// <summary>
        /// Reads and returns <see cref="System.Char" />.
        /// </summary>
        public char ReadChar()
        {
            return (char)ReadUInt16();
        }

        /// <summary>
        /// Reads and returns <see cref="System.Boolean" />.
        /// </summary>
        public bool ReadBoolean()
        {
            return ReadByte() == 1;
        }

        /// <summary>
        /// Reads and returns <see cref="System.Guid" />.
        /// </summary>
        public Guid ReadGuid()
        {
            return new Guid(ReadBytes(16));
        }

        /// <summary>
        /// Reads and returns string.
        /// </summary>
        public string ReadString()
        {
            bool fake;
            var length = ReadInt32(); // length prefix
            var chunk = InnerChunkRead(length, out fake);
            return Encoding.UTF8.GetString(chunk.Array, chunk.Offset, chunk.Count);
        }

        /// <summary>
        /// Reads the <see cref="System.Decimal"/> .
        /// </summary>
        public decimal ReadDecimal()
        {
            var lo = ReadInt32();
            var mid = ReadInt32();
            var hi = ReadInt32();
            var scale = ReadInt32();
            return new Decimal(lo, mid, hi, 
                (scale & DecimalSignMask) != 0,
                (byte)((scale & DecimalScaleMask) >> DecimalScaleShift));
        }
        /// <summary>
        /// Reads the given number of bytes.
        /// </summary>
        /// <returns>
        /// The array holding read bytes.
        /// </returns>
        /// <param name='count'>
        /// Number of bytes to read.
        /// </param>
        public byte[] ReadBytes(int count)
        {
            bool bufferCreated;
            var chunk = InnerChunkRead(count, out bufferCreated);
            if(bufferCreated)
            {
                return chunk.Array;
            }
            var result = new byte[count];
            Array.Copy(chunk.Array, chunk.Offset, result, 0, chunk.Count);
            return result;
        }

        /// <summary>
        /// Copies given number of bytes to a given stream.
        /// </summary>
        /// <param name='destination'>
        /// Writeable stream to which data will be copied.
        /// </param>
        /// <param name='howMuch'>
        /// The number of bytes which will be copied to the destination stream.
        /// </param>
        public void CopyTo(Stream destination, long howMuch)
        {
            var localBuffer = new byte[Helpers.MaximalPadding];
            if(buffered)
            {
                // first we need to flush the inner buffer into a stream
                var dataLeft = currentBufferSize - currentBufferPosition;
                var toRead = (int)Math.Min(dataLeft, howMuch);
                destination.Write(buffer, currentBufferPosition, toRead);
                currentBufferPosition += toRead;
                howMuch -= toRead;
                if(howMuch <= 0)
                {
                    return;
                }
            }
            // we can reuse the regular buffer since it is invalidated at this point anyway
            int read;
            while((read = stream.Read(localBuffer, 0, (int)Math.Min(localBuffer.Length, howMuch))) > 0)
            {
                howMuch -= read;
                destination.Write(localBuffer, 0, read);
                currentPosition += read;
            }
            if(howMuch > 0)
            {
                throw new EndOfStreamException(string.Format("End of stream reached while {0} more bytes expected.", howMuch));
            }
        }

        /// <summary>
        /// After this call stream's position is updated to match the padding used by <see cref="Antmicro.Migrant.PrimitiveWriter"/>.
        /// It is needed to be called if one expects consecutive reads (of data written previously by consecutive writes). It is not necessary
        /// to call this method when buffering is not used.
        /// </summary>
        /// <remarks>
        /// Call <see cref="Dispose"/> when you are finished using the <see cref="Antmicro.Migrant.PrimitiveReader"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Antmicro.Migrant.PrimitiveReader"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the
        /// <see cref="Antmicro.Migrant.PrimitiveReader"/> so the garbage collector can reclaim the memory that the
        /// <see cref="Antmicro.Migrant.PrimitiveReader"/> was occupying.
        /// </remarks>
        public void Dispose()
        {
            if(!buffered)
            {
                return;
            }
            // we have to leave the stream in aligned position
            var toRead = Helpers.GetCurrentPaddingValue(currentPosition);
            stream.ReadOrThrow(buffer, 0, toRead);
        }

        private ulong InnerReadInteger()
        {
            ulong next;
            var result = 0UL;
#if DEBUG_FORMAT
            if(Serializer.DisableVarints)
            {
                for(int i = 0; i < sizeof(ulong); ++i)
                {
                    next = ReadByte();
                    result |= (next << 8 * (sizeof(ulong) - i - 1));
                }

                return result;
            }
#endif
            var shift = 0;
            do
            {
                next = ReadByte();
                result |= (next & 0x7FU) << shift;
                shift += 7;
            }
            while((next & 128) > 0);
            return result;
        }

        private void CheckBuffer()
        {
            if(currentBufferSize <= currentBufferPosition)
            {
                ReloadBuffer();
            }
        }

        private void ReloadBuffer()
        {
            // how much can we read?
            var toRead = Helpers.GetNextBytesToRead(currentPosition);
            stream.ReadOrThrow(buffer, 0, toRead);
            currentPosition += toRead;
            currentBufferSize = toRead;
            currentBufferPosition = 0;
        }

        private ArraySegment<byte> InnerChunkRead(int byteNumber, out bool bufferCreated)
        {
            if(!buffered)
            {
                bufferCreated = true;
                var data = new byte[byteNumber];
                stream.ReadOrThrow(data, 0, byteNumber);
                currentBufferPosition += byteNumber;
                return new ArraySegment<byte>(data);
            }
            bufferCreated = false;
            var dataLeft = currentBufferSize - currentBufferPosition;
            if(byteNumber > dataLeft)
            {
                var data = new byte[byteNumber];
                bufferCreated = true;
                var toRead = byteNumber - dataLeft;
                Array.Copy(buffer, currentBufferPosition, data, 0, dataLeft);
                currentBufferPosition += dataLeft;
                stream.ReadOrThrow(data, dataLeft, toRead);
                currentPosition += toRead;
                return new ArraySegment<byte>(data, 0, byteNumber);
            }
            var result = new ArraySegment<byte>(buffer, currentBufferPosition, byteNumber);
            currentBufferPosition += byteNumber;
            return result;
        }

        /*
		 * Since we want the shift in zigzag decoding to be unsigned shift, we simulate it here, turning off
		 * the most significant bit (which is always zero in unsigned shift).
		 */
        private const int AllButMostSignificantShort = unchecked((short)~(1 << 15));
        private const int AllButMostSignificantInt = ~(1 << 31);
        private const long AllButMostSignificantLong = ~(1L << 63);

        private const int DecimalScaleMask = 0x00FF0000;
        private const int DecimalScaleShift = 16;
        private const uint DecimalSignMask = 0x80000000;

        private long currentPosition;
        private readonly byte[] buffer;
        private int currentBufferSize;
        private int currentBufferPosition;
        private readonly Stream stream;
        private readonly bool buffered;
    }
}

