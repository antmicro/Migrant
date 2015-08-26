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
namespace Antmicro.Migrant
{
    /// <summary>
    /// Enumeration describing possible desarialization operation results.
    /// </summary>
    public enum DeserializationResult
    {
        /// <summary>
        /// Deserialization succeeded.
        /// </summary>
        OK,
        /// <summary>
        /// Magic number was different than expected.
        /// </summary>
        WrongMagic,
        /// <summary>
        /// Serializer version mismatch. 
        /// </summary>
        WrongVersion,
        /// <summary>
        /// The type structure has changed in a not allowed way.
        /// </summary>
        TypeStructureChanged,
        /// <summary>
        /// Data in a stream was corrupted.
        /// </summary>
        StreamCorrupted,
        /// <summary>
        /// Type stamping configuration is inconsistent between stream and deserializer settings.
        /// </summary>
        WrongStreamConfiguration
    }
}

