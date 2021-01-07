//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using Antmicro.Migrant.Customization;
using System.IO;
using System.Reflection;
using Antmicro.Migrant.VersionTolerance;
using System.Linq;

namespace Antmicro.Migrant.Tests
{
    public class TwoDomainsDriver
    {
        public TwoDomainsDriver(bool useGeneratedSerializer, bool useGeneratedDeserializer)
        {
            settings = new DriverSettings(useGeneratedSerializer, useGeneratedDeserializer, true);

            // this is just a little hack that allows us to debug TwoDomainTests on one domain
            // when `PrepareDomains` method is not called; when `PrepareDomains` is called
            // then these fields are overwritten with proper values
            testsOnDomain1 = new InnerDriver { Settings = settings };
            testsOnDomain2 = testsOnDomain1;
        }

        public void PrepareDomains()
        {
            var pathBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var domain in new[] { "domain1", "domain2" }.Select(x => Path.Combine(pathBase, x)))
            {
                Directory.CreateDirectory(domain);
                foreach(var file in new[] { "Tests.dll", "Migrant.dll" })
                {
                    File.Copy(Path.Combine(pathBase, file), Path.Combine(domain, file), true);
                }
            }

            domain1 = AppDomain.CreateDomain("domain1", null, Path.Combine(pathBase, "domain1"), string.Empty, false);
            domain2 = AppDomain.CreateDomain("domain2", null, Path.Combine(pathBase, "domain2"), string.Empty, false);

            testsOnDomain1 = (InnerDriver)domain1.CreateInstanceAndUnwrap(typeof(InnerDriver).Assembly.FullName, typeof(InnerDriver).FullName);
            testsOnDomain2 = (InnerDriver)domain2.CreateInstanceAndUnwrap(typeof(InnerDriver).Assembly.FullName, typeof(InnerDriver).FullName);

            testsOnDomain1.Settings = settings;
            testsOnDomain2.Settings = settings;
        }

        public void DisposeDomains()
        {
            testsOnDomain1 = null;
            testsOnDomain2 = null;
            AppDomain.Unload(domain1);
            AppDomain.Unload(domain2);
            domain1 = null;
            domain2 = null;

            var pathBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Directory.Delete(Path.Combine(pathBase, "domain1"), true);
            Directory.Delete(Path.Combine(pathBase, "domain2"), true);
        }
        
        public bool SerializeAndDeserializeOnTwoAppDomains(DynamicType domainOneType, DynamicType domainTwoType, VersionToleranceLevel vtl, bool allowGuidChange = true)
        {
            testsOnDomain1.CreateInstanceOnAppDomain(domainOneType);
            testsOnDomain2.CreateInstanceOnAppDomain(domainTwoType);

            var bytes = testsOnDomain1.SerializeOnAppDomain();
            try 
            {
                testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettings(allowGuidChange ? vtl | VersionToleranceLevel.AllowGuidChange : vtl));
                return true;
            } 
            catch (VersionToleranceException)
            {
                return false;
            }
        }

        protected DriverSettings settings;

        protected InnerDriver testsOnDomain1;
        protected InnerDriver testsOnDomain2;
        
        private AppDomain domain1;
        private AppDomain domain2;
    }

    public class DriverSettings : MarshalByRefObject
    {
        public DriverSettings(bool useGeneratedSerializer, bool useGeneratedDeserializer, bool useStamping)
        {
            this.useGeneratedDeserializer = useGeneratedDeserializer;
            this.useGeneratedSerializer = useGeneratedSerializer;
            this.useStamping = useStamping;
        }

        public Settings GetSettings(VersionToleranceLevel level = 0)
        {
            return new Settings(useGeneratedSerializer ? Method.Generated : Method.Reflection,
                                useGeneratedDeserializer ? Method.Generated : Method.Reflection,
                                level,
                                disableTypeStamping: !useStamping);
        }

        public Settings GetSettingsAllowingGuidChange(VersionToleranceLevel level = 0)
        {
            return GetSettings(level | VersionToleranceLevel.AllowGuidChange);
        }

        private bool useGeneratedSerializer;
        private bool useGeneratedDeserializer;
        private bool useStamping;
    }
    
    public class InnerDriver : MarshalByRefObject
    {
        public InnerDriver()
        {
            DynamicType.prefix = AppDomain.CurrentDomain.FriendlyName;
        }

        public void CreateInstanceOnAppDomain(DynamicType type, Version version = null)
        {
            obj = type.Instantiate(version);
        }

        public void DeserializeOnAppDomain(byte[] data, Settings settings)
        {
            var stream = new MemoryStream(data);
            var deserializer = new Serializer(settings);
            obj = deserializer.Deserialize<object>(stream);
        }

        public void SetValueOnAppDomain(string className, string fieldName, object value)
        {
            var field = FindClass(className).GetField(fieldName);
            field.SetValue(obj, value);
        }

        public void SetValueOnAppDomain(string fieldName, object value)
        {
            SetValueOnAppDomainInner(obj, fieldName, value);
        }

        private object SetValueOnAppDomainInner(object o, string fieldName, object value)
        {
            var elements = fieldName.Split(new[] { '.' }, 2);
            if (elements.Length == 1)
            {
                var field = o.GetType().GetField(fieldName);
                field.SetValue(o, value);
                return o;
            }
            else
            {
                var field = o.GetType().GetField(elements[0]);
                var local = field.GetValue(o);
                local = SetValueOnAppDomainInner(local, elements[1], value);
                field.SetValue(o, local);
                return o;
            }
        }

        public object GetValueOnAppDomain(string className, string fieldName)
        {
            var field = FindClass(className).GetField(fieldName);
            return field.GetValue(obj);
        }

        public object GetValueOnAppDomain(string fieldName)
        {
            var elements = fieldName.Split('.');
            FieldInfo field = null;
            object currentObject = null;
            foreach (var element in elements)
            {
                currentObject = (currentObject == null) ? obj : field.GetValue(currentObject);
                field = currentObject.GetType().GetField(element);
            }

            return field.GetValue(currentObject);
        }

        public byte[] SerializeOnAppDomain()
        {
            var stream = new MemoryStream();
            var serializer = new Serializer(Settings.GetSettings());
            serializer.Serialize(obj, stream);
            return stream.ToArray();
        }
            
        private Type FindClass(string className)
        {
            var currentClass = obj.GetType();
            while (currentClass != null && currentClass.Name != className)
            {
                currentClass = currentClass.BaseType;
            }
            if (currentClass == null)
            {
                throw new ArgumentException(className);
            }

            return currentClass;
        }

        public string Location { get; set; }
        public DriverSettings Settings { get; set; }

        protected object obj;
    }
}

