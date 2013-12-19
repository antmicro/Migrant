/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)

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

namespace Antmicro.Migrant.Customization
{
	/// <summary>
	/// Level of the version tolerance, that is how much layout of the deserialized type can differ
	/// from the (original) layout of the serialized type.
	/// </summary>
	public enum VersionToleranceLevel
	{
		/// <summary>
		/// Both new and old layout have to have the same number of fields, with the same names and types.
		/// </summary>
		Exact = 0,

		/// <summary>
		/// Both serialized and serialized classes must have identical module ID (which is GUID). In other words
		/// that means these assemblies are from one and the same compilation.
		/// </summary>
		Guid,

		/// <summary>
		/// The new layout can have more fields that the old one. They are initialized to their default values.
		/// </summary>
		FieldAddition,

		/// <summary>
		/// The new layout can have less fields that the old one. Values of the missing one are ignored.
		/// </summary>
		FieldRemoval,

		/// <summary>
		/// The new layout can have some fields that are missing in the old one and vice versa. Additional
		/// fields are initialized to their default values, and serialized values of the missing fields are
		/// ignored.
		/// </summary>
		FieldAdditionAndRemoval
	}
}

