//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;


namespace Antmicro.Migrant.Customization
{
    /// <summary>
    /// Contains serialization settings.
    /// </summary>
    /// 	
    // The reason settings are serializable is to enable tests regarding version tolerant serialization
    [Serializable]
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
        /// Specifies how much the layout of the serialized class can differ from the version
        /// that is available when that data is deserialized.
        /// </summary>
        public VersionToleranceLevel VersionTolerance { get; private set; }

        /// <summary>
        /// Specifies whether Migrant should use GetObjectData approach for serialization.
        /// </summary>
        /// <value><c>true</c> if support for ISerializable is active; otherwise, <c>false</c>.</value>
        public bool SupportForISerializable { get; private set; }

        /// <summary>
        /// Specifies whether Migrant should utilize IXmlSerializable interface.
        /// </summary>
        /// <value><c>true</c> if support for IXmlSserializable is active; otherwise, <c>false</c>.</value>
        public bool SupportForIXmlSerializable { get; private set; }

        /// <summary>>
        /// Gets the value of flag specifing if collection objects are to be deserialized without optimization (treated as normal user objects).
        /// </summary>
        public bool TreatCollectionAsUserObject { get; private set; }

        /// <summary>
        /// Gets a value indicating whether buffering is used. Without buffering, all writes are going
        /// directly to underlying and stream and no padding is used. Reads behave accordingly.
        /// </summary>
        /// <value><c>true</c> if buffering is used; otherwise, <c>false</c>.</value>
        public bool UseBuffering { get; private set; }

        /// <summary>
        /// Tells serializer how to treat identity of objects between sessions of open
        /// stream serialization.
        /// </summary>
        public ReferencePreservation ReferencePreservation { get; private set; }

        /// <summary>
        /// Specifies if type stamping should be disabled in order to improve performance and limit output stream size.
        /// </summary>
        /// <value><c>true</c> if disable type stamping; otherwise, <c>false</c>.</value>
        public bool DisableTypeStamping { get; private set; }

        /// <summary>
        /// Specifies if type stamps should be compared even when GUID has not changed.
        /// This is used in internal framework's tests.
        /// </summary>
        public bool ForceStampVerification { get; private set ;}

        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.Customization.Settings"/> class.
        /// </summary>
        /// <param name='serializationMethod'>
        /// Method used for serialization.
        /// </param>
        /// <param name='deserializationMethod'>
        /// Method used for deserialization.
        /// </param>
        /// <param name='versionTolerance'>
        /// Specifies the possible level of difference between class layout when it was serialized and in the
        /// moment of deserialization.
        /// </param>
        /// <param name = "treatCollectionAsUserObject">
        /// Specifies if collection objects are to be deserialized without optimization (treated as normal user objects).
        /// </param>
        /// <param name = "supportForISerializable">
        /// Specifies whether Migrant should use GetObjectData approach for serialization.
        /// </param>
        /// <param name="supportForIXmlSerializable"> 
        /// Specifies whether Migrant should use xml serialization on objects implementing IXmlSerializable.
        /// </param>
        /// <param name="useBuffering"> 
        /// True if buffering should be used, false if writes should directly go to the stream and reads should never read
        /// data in advance. Disabling buffering also disables padding.
        /// </param>
        /// <param name="disableTypeStamping"> 
        /// Specifies if type stamping should be disabled in order to improve performance and limit output stream size.
        /// </param>
        /// <param name="referencePreservation"> 
        /// Tells serializer how to treat references between sessions of open stream serialization.
        /// </param>
        /// <param name="forceStampVerification">
        /// Specifies if type stamps should be compared even when GUID has not changed.
        /// </param>
        public Settings(Method serializationMethod = Method.Generated, Method deserializationMethod = Method.Generated, VersionToleranceLevel versionTolerance = 0,
                        bool supportForISerializable = false, bool supportForIXmlSerializable = false, bool treatCollectionAsUserObject = false,
                        bool useBuffering = true, bool disableTypeStamping = false, ReferencePreservation referencePreservation = ReferencePreservation.Preserve, bool forceStampVerification = false)
        {
            SerializationMethod = serializationMethod;
            DeserializationMethod = deserializationMethod;
            VersionTolerance = versionTolerance;
            SupportForISerializable = supportForISerializable;
            SupportForIXmlSerializable = supportForIXmlSerializable;
            TreatCollectionAsUserObject = treatCollectionAsUserObject;
            UseBuffering = useBuffering;
            ReferencePreservation = referencePreservation;
            DisableTypeStamping = disableTypeStamping;
            ForceStampVerification = forceStampVerification;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.Customization.Settings"/> class.
        /// </summary>
        /// <param name='serializationMethod'>
        /// Method used for serialization.
        /// </param>
        /// <param name='deserializationMethod'>
        /// Method used for deserialization.
        /// </param>
        /// <param name='versionTolerance'>
        /// Specifies the possible level of difference between class layout when it was serialized and in the
        /// moment of deserialization.
        /// </param>
        /// <param name = "treatCollectionAsUserObject">
        /// Specifies if collection objects are to be deserialized without optimization (treated as normal user objects).
        /// </param>
        /// <param name = "supportForISerializable">
        /// Specifies whether Migrant should use GetObjectData approach for serialization.
        /// </param>
        /// <param name="useBuffering"> 
        /// True if buffering should be used, false if writes should directly go to the stream and reads should never read
        /// data in advance. Disabling buffering also disables padding.
        /// </param>
        /// <param name="disableTypeStamping"> 
        /// Specifies if type stamping should be disabled in order to improve performance and limit output stream size.
        /// </param>
        /// <param name="referencePreservation"> 
        /// Tells serializer how to treat references between sessions of open stream serialization.
        /// </param>
        /// <param name="forceStampVerification">
        /// Specifies if type stamps should be compared even when GUID has not changed.
        /// </param>
        public Settings With(Method? serializationMethod = null, Method? deserializationMethod = null, VersionToleranceLevel? versionTolerance = null,
                             bool? supportForISerializable = null, bool? treatCollectionAsUserObject = null, bool? useBuffering = null, bool? disableTypeStamping = null,
                             ReferencePreservation? referencePreservation = null, bool? forceStampVerification = null)
        {
            var result = new Settings {
                SerializationMethod = serializationMethod ?? SerializationMethod,
                DeserializationMethod = deserializationMethod ?? DeserializationMethod,
                VersionTolerance = versionTolerance ?? VersionTolerance,
                SupportForISerializable = supportForISerializable ?? SupportForISerializable,
                TreatCollectionAsUserObject = treatCollectionAsUserObject ?? TreatCollectionAsUserObject,
                UseBuffering = useBuffering ?? UseBuffering,
                DisableTypeStamping = disableTypeStamping ?? DisableTypeStamping,
                ReferencePreservation = referencePreservation ?? ReferencePreservation,
                ForceStampVerification = forceStampVerification ?? ForceStampVerification
            };
            return result;
        }
    }
}

