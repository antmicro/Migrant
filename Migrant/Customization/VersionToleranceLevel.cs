using System;

namespace AntMicro.Migrant.Customization
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
		GUID,

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

