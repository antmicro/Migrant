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
namespace AntMicro.Migrant
{
	/// <summary>
	/// When implemented, its method are used by serializer to save or load the state of
	/// the object. Implementing class must contain a parameterless constructor which is
	/// invoked during deserialization, immediately before calling the <see cref="Load" />
	/// method.
	/// </summary>
	public interface ISpeciallySerializable
	{
		/// <summary>
		/// Invoked by the serializer immediately after construction of object (using
		/// parameterless constructor). Should restore the state of the object, previously
		/// stored using the <see cref="Save" /> method.
		/// </summary>
		/// <param name='reader'>
		/// The reader which should be used to read the previously written data.
		/// </param>
		/// <remarks>
		/// This method must read the number of bytes that were earlier written. Otherwise
		/// the <see cref="System.InvalidOperationException" /> is thrown.
		/// </remarks>
		void Load(PrimitiveReader reader);

		/// <summary>
		/// Invoked by the serializer during serialization. Should store the state of the
		/// object which can be later read using the <see cref="Load" /> method.
		/// </summary>
		/// <param name='writer'>
		/// Writer used to save the state of the object.
		/// </param>
		void Save(PrimitiveWriter writer);
	}
}

