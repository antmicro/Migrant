using System;
using System.IO;
using System.Text;

namespace AntMicro.Migrant
{
    public class PrimitiveReader : IDisposable
    {
        public PrimitiveReader(Stream stream)
        {
            this.stream = stream;
            // buffer size is the size of the maximal padding
            buffer = new byte[Helpers.MaximalPadding];
        }

        public long Position
        {
            get
            {
                return currentPosition - currentBufferSize + currentBufferPosition;
            }
        }

        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        public float ReadSingle()
        {
            return (float)BitConverter.Int64BitsToDouble(ReadInt64());
        }

        public DateTime ReadDateTime()
        {
            return Helpers.DateTimeEpoch.AddTicks(ReadInt64());
        }

        public TimeSpan ReadTimeSpan()
        {
            return TimeSpan.FromTicks(ReadInt64());
        }

        public byte ReadByte()
        {
            CheckBuffer();
            return buffer[currentBufferPosition++];
        }

        public sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        public short ReadInt16()
        {
            return (short)InnerReadInteger();
        }

        public ushort ReadUInt16()
        {
            return (ushort)InnerReadInteger();
        }

        public int ReadInt32()
        {
            return (int)InnerReadInteger();
        }

        public uint ReadUInt32()
        {
            return (uint)InnerReadInteger();
        }

        public char ReadChar()
        {
            return (char)ReadUInt16();
        }

        public bool ReadBool()
        {
            return ReadByte() == 1;
        }

        public long ReadInt64()
        {
            return (long)InnerReadInteger();
        }

        public ulong ReadUInt64()
        {
            return InnerReadInteger();
        }

        public string ReadString()
        {
            bool fake;
            var length = ReadInt32(); // length prefix
            var chunk = InnerChunkRead(length, out fake);
            return Encoding.UTF8.GetString(chunk.Array, chunk.Offset, chunk.Count);
        }

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

        public void CopyTo(Stream destination, long howMuch)
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
            // we can reuse the regular buffer since it is invalidated at this point anyway
            int read;
            while((read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, howMuch))) > 0)
            {
                howMuch -= read;
                destination.Write(buffer, 0, read);
                currentPosition += read;
            }
        }

        public void Dispose()
        {
            // we have to leave stream in aligned position
            var toRead = Helpers.GetCurrentPaddingValue(currentPosition);
            stream.ReadOrThrow(buffer, 0, toRead);
        }

        private ulong InnerReadInteger()
        {
            ulong next;
            var result = 0UL;
            var shift = 0;
            do
            {
                next = ReadByte();
                result |= (next & 0x7FU) << shift;
                shift += 7;
            } while((next & 128) > 0);
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

        private long currentPosition;
        private readonly byte[] buffer;
        private int currentBufferSize;
        private int currentBufferPosition;
        private readonly Stream stream;
    }
}

