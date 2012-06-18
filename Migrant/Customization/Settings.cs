using System;

namespace Migrant.Customization
{
	public sealed class Settings
	{
		public Method SerializationMethod { get; set; }
		public Method DeserializationMethod { get; set; }

		public Settings()
		{
			SerializationMethod = Method.Generated;
			DeserializationMethod = Method.Reflection; // TODO: change to generated when it's done
		}
	}
}

