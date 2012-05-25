using System;
using System.IO;
using System.Text;

namespace AntMicro.Migrant
{
    public class PrimitiveWriter : IDisposable
    {
        public PrimitiveWriter(Stream stream)
        {
            this.stream = stream;
            buffer = new byte[BufferSize];
        }

        public long Position
        {
            get
            {
                return currentPosition + currentBufferPosition;
            }
        }

        public void Write(double value)
        {
            Write(BitConverter.DoubleToInt64Bits(value));
        }

        public void Write(float value)
        {
            Write(BitConverter.DoubleToInt64Bits(value));
        }

        public void Write(DateTime value)
        {
            Write(value - Helpers.DateTimeEpoch);
        }

        public void Write(TimeSpan value)
        {
            Write(value.Ticks);
        }

        public void Write(byte value)
        {
            CheckBuffer(1);
            buffer[currentBufferPosition++] = value;
        }

        public void Write(sbyte value)
        {
            Write((byte)value);
        }

        public void Write(short value)
        {
            InnerWriteInteger((ushort)value, sizeof(int));
        }

        public void Write(ushort value)
        {
            InnerWriteInteger(value, sizeof(int));
        }

        public void Write(int value)
        {
            InnerWriteInteger((uint)value, sizeof(int));
        }

        public void Write(uint value)
        {
            InnerWriteInteger(value, sizeof(int));
        }

        public void Write(char value)
        {
            Write((ushort)value);
        }

        public void Write(bool value)
        {
            Write((byte)(value ? 1 : 0));
        }

        public void Write(long value)
        {
            Write((ulong)value);
        }

        public void Write(ulong value)
        {
            InnerWriteInteger(value, sizeof(ulong) + 1);
        }

        public void Write(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            Write(bytes.Length);
            InnerChunkWrite(bytes);
        }

        public void Write(byte[] bytes)
        {
            InnerChunkWrite(bytes);
        }

        public void CopyFrom(Stream source, long howMuch)
        {
            Flush();
            // we can reuse the regular buffer since it is flushed at this point anyway
            int read;
            while((read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, howMuch))) > 0)
            {
                howMuch -= read;
                currentPosition += read;
                stream.Write(buffer, 0, read);
            }
        }

        public void Dispose()
        {
            Flush();
            Pad();
        }

        private void InnerWriteInteger(ulong value, int sizeInBytes)
        {
            CheckBuffer(sizeInBytes + 1);
            while(value > 127)
            {
                buffer[currentBufferPosition] = (byte)(value | 128);
                value >>= 7;
                currentBufferPosition++;
            }
            buffer[currentBufferPosition] = (byte)(value & 127);
            currentBufferPosition++;
        }

        private void InnerChunkWrite(byte[] data)
        {
            var length = data.Length;
            CheckBuffer(length);
            if(length > BufferSize)
            {
                stream.Write(data, 0, data.Length);
                currentPosition += data.Length;
            } 
            else
            {
                data.CopyTo(buffer, currentBufferPosition);
                currentBufferPosition += length;
            }
        }

        // TODO: inline maybe?
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
            stream.Write(buffer, 0, currentBufferPosition);
            currentPosition += currentBufferPosition;
            currentBufferPosition = 0;
        }

        private void Pad()
        {
            var bytesToPad = Helpers.GetCurrentPaddingValue(currentPosition);
            var pad = new byte[bytesToPad];
            stream.Write(pad, 0, pad.Length);
        }

        private readonly byte[] buffer;
        private int currentBufferPosition;
        private long currentPosition;
        private readonly Stream stream;
        private const int BufferSize = 4 * 1024;
    }
}

