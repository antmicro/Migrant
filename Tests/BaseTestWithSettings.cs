/*
  Copyright (c) 2013 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
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
using Antmicro.Migrant.Customization;

namespace Antmicro.Migrant.Tests
{
	public abstract class BaseTestWithSettings
	{
    protected BaseTestWithSettings(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool treatCollectionsAsUserObjects, bool supportForISerializable)
		{
			this.useGeneratedSerializer        = useGeneratedSerializer;
			this.useGeneratedDeserializer      = useGeneratedDeserializer;
      this.treatCollectionsAsUserObjects = treatCollectionsAsUserObjects;
      this.supportForISerializable       = supportForISerializable;
		}

		protected Settings GetSettings(VersionToleranceLevel level = 0)
		{
			return new Settings(useGeneratedSerializer ? Method.Generated : Method.Reflection,					
                                useGeneratedDeserializer ? Method.Generated : Method.Reflection,    
                                level,
                                supportForISerializable,
                                treatCollectionsAsUserObjects);
		}

		protected T SerializerClone<T>(T toClone)
		{
			return Serializer.DeepClone(toClone, GetSettings());
		}

		private readonly bool useGeneratedSerializer;
		private readonly bool useGeneratedDeserializer;
    private readonly bool treatCollectionsAsUserObjects;
    private readonly bool supportForISerializable;
	}
}
