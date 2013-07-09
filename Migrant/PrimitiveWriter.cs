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
using System.IO;
using System.Text;

namespace AntMicro.Migrant
{
	/// <summary>
	/// Provides the mechanism for writing primitive values into a stream.
	/// </summary>
	/// <remarks>
	/// Can be used as a replacement for the <see cref="System.IO.BinaryWriter" /> . Provides
	/// more compact output and reads no more data from the stream than requested. Although
	/// the underlying format is not specified at this point, it is guaranteed to be consistent with
	/// <see cref="AntMicro.Migrant.PrimitiveReader" />. Writer has to be disposed after used,
	/// otherwise stream position corruption and data loss can occur. Writer does not possess the
	/// stream and does not close it after dispose.
	/// </remarks>
	public sealed class PrimitiveWriter : IDisposable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.PrimitiveWriter" /> class.
		/// </summary>
		/// <param name='stream'>
		/// The underlying stream which will be used to write data. Has to be writeable.
		/// </param>
		public PrimitiveWriter(Stream stream)
		{
			this.stream = stream;
			buffer = new byte[BufferSize];
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
			Write(value.Ticks);
		}

		/// <summary>
		/// Writes the specified value of type <see cref="System.Byte" />.
		/// </summary>
		public void Write(byte value)
		{
			CheckBuffer(1);
			buffer[currentBufferPosition++] = value;
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
			if(PrimitiveWriter.DontUseIntegerCompression)
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
			if(PrimitiveWriter.DontUseIntegerCompression)
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
			if(PrimitiveWriter.DontUseIntegerCompression)
			{
				Write((ulong)value);
				return;
			}
#endif
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

		/// <summary>
		/// Flushes the buffer and pads the stream with sufficient amount of data to be compatible 
		/// with the <see cref="AntMicro.Migrant.PrimitiveReader" />.
		/// </summary>
		/// <remarks>
		/// Call <see cref="Dispose"/> when you are finished using the <see cref="AntMicro.Migrant.PrimitiveWriter"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="AntMicro.Migrant.PrimitiveWriter"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="AntMicro.Migrant.PrimitiveWriter"/> so the garbage collector can reclaim the memory that the
		/// <see cref="AntMicro.Migrant.PrimitiveWriter"/> was occupying.
		/// </remarks>
		public void Dispose()
		{
			Flush();
			Pad();
		}

		private void InnerWriteInteger(ulong value, int sizeInBytes)
		{
#if DEBUG
			if(DontUseIntegerCompression)
			{
				CheckBuffer(sizeof(ulong));
				ulong current = 0;
				for(int i = 0; i < sizeof(ulong); ++i)
				{
					current = value & 255;
					buffer[currentBufferPosition + sizeof(ulong) - i - 1] = (byte)current;
					value >>= 8;
				}

				currentBufferPosition += sizeof(ulong);
				return;
			}
#endif
			CheckBuffer(sizeInBytes);
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
			InnerChunkWrite(data, 0, data.Length);
		}

		private void InnerChunkWrite(byte[] data, int offset, int length)
		{
			CheckBuffer(length);
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

#if DEBUG
		internal static readonly bool DontUseIntegerCompression = false;
#endif
	}
}

