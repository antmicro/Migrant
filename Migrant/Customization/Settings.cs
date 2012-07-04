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
namespace AntMicro.Migrant.Customization
{
	/// <summary>
	/// Contains serialization settings.
	/// </summary>
	public sealed class Settings
	{
		/// <summary>
		/// Gets the method used for serialization.
		/// </summary>
		public Method SerializationMethod { get; private set; }

		/// <summary>
		/// Gets the method used for deserialization.
		/// </summary>
		public Method DeserializationMethod { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.Customization.Settings"/> class with default settings.
		/// </summary>
		public Settings()
		{
			SerializationMethod = Method.Generated;
			DeserializationMethod = Method.Reflection; // TODO: change to generated when it's done
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntMicro.Migrant.Customization.Settings"/> class.
		/// </summary>
		/// <param name='serializationMethod'>
		/// Method used for serialization.
		/// </param>
		/// <param name='deserializationMethod'>
		/// Method used for deserialization.
		/// </param>
		public Settings(Method serializationMethod, Method deserializationMethod)
		{
			SerializationMethod = serializationMethod;
			DeserializationMethod = deserializationMethod;
		}
	}
}
