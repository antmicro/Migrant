//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using Antmicro.Migrant.Customization;
using System.IO;

namespace Antmicro.Migrant.Tests
{
    public abstract class BaseTestWithSettings
    {
        protected BaseTestWithSettings(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool treatCollectionsAsUserObjects, bool supportForISerializable,
            bool supportForIXmlSerializable, bool useTypeStamping, bool forceStampVerification)
        {
            this.useGeneratedSerializer = useGeneratedSerializer;
            this.useGeneratedDeserializer = useGeneratedDeserializer;
            this.treatCollectionsAsUserObjects = treatCollectionsAsUserObjects;
            this.supportForISerializable = supportForISerializable;
            this.supportForIXmlSerializable = supportForIXmlSerializable;
            this.useTypeStamping = useTypeStamping;
            this.forceStampVerification = forceStampVerification;
        }

        protected Settings GetSettings(VersionToleranceLevel level = 0)
        {
            return new Settings(useGeneratedSerializer ? Method.Generated : Method.Reflection,					
                useGeneratedDeserializer ? Method.Generated : Method.Reflection,    
                level,
                supportForISerializable,
                supportForIXmlSerializable,
                treatCollectionsAsUserObjects,
                disableTypeStamping: !useTypeStamping,
                forceStampVerification: forceStampVerification);
        }

        protected T SerializerClone<T>(T toClone)
        {
            return Serializer.DeepClone(toClone, GetSettings());
        }

        // pseudo, because cloned object can be/can contain different type due to surrogate operations
        protected object PseudoClone(object obj, Action<Serializer> actionsBeforeSerilization)
        {
            var serializer = new Serializer(GetSettings());
            actionsBeforeSerilization(serializer);
            var mStream = new MemoryStream();
            serializer.Serialize(obj, mStream);
            mStream.Seek(0, SeekOrigin.Begin);
            return serializer.Deserialize<object>(mStream);
        }

        protected readonly bool useGeneratedSerializer;

        private readonly bool useGeneratedDeserializer;
        private readonly bool treatCollectionsAsUserObjects;
        private readonly bool supportForISerializable;
        private readonly bool supportForIXmlSerializable;
        private readonly bool useTypeStamping;
        private readonly bool forceStampVerification;
    }
}
