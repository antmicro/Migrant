//
// Copyright (c) 2012-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;

namespace Antmicro.Migrant.Tests.Shared
{
    public interface IInnerDriver
    {
        public void CreateInstanceOnAppDomain(DynamicType type, Version version = null);

        public void DeserializeOnAppDomain(byte[] data, SettingsGenerator settings);

        public void SetValueOnAppDomain(string className, string fieldName, object value);

        public void SetValueOnAppDomain(string fieldName, object value);

        public object GetValueOnAppDomain(string className, string fieldName);

        public object GetValueOnAppDomain(string fieldName);

        public byte[] SerializeOnAppDomain();

        public string Location { get; set; }

        public DriverSettings Settings { get; set; }

        public class DriverSettings
        {
            public DriverSettings()
            {
            }

            public DriverSettings(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useStamping)
            {
                this.useGeneratedDeserializer = useGeneratedDeserializer;
                this.useGeneratedSerializer = useGeneratedSerializer;
                this.useStamping = useStamping;
            }

            public SettingsGenerator GetSettings(ulong level = 0)
            {
                return new SettingsGenerator(useGeneratedSerializer,
                                    useGeneratedDeserializer,
                                    useStamping,
                                    level);
            }

            public SettingsGenerator GetSettingsAllowingGuidChange(ulong level = 0)
            {
                return GetSettings(level | 0x1);
            }

            public bool useGeneratedSerializer;
            public bool useGeneratedDeserializer;
            public bool useStamping;
        }

        public class SettingsGenerator
        {
            public SettingsGenerator(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useStamping, ulong level = 0)
            {
                this.UseGeneratedDeserializer = useGeneratedDeserializer;
                this.UseGeneratedSerializer = useGeneratedSerializer;
                this.UseStamping = useStamping;
                this.Level = level;
            }

            public readonly bool UseGeneratedSerializer;
            public readonly bool UseGeneratedDeserializer;
            public readonly bool UseStamping;
            public readonly ulong Level;
        }
    }
}
