//
// Copyright (c) 2012-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.Loader;

using Antmicro.Migrant.Customization;
using Antmicro.Migrant.Tests.Shared;

namespace Antmicro.Migrant.Tests
{
    public class TwoDomainsDriver
    {
        public TwoDomainsDriver(bool useGeneratedSerializer, bool useGeneratedDeserializer)
        {
            settings = new IInnerDriver.DriverSettings(useGeneratedSerializer, useGeneratedDeserializer, true);

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

            domain1 = new DomainLoadContext("domain1", Path.Combine(pathBase, "domain1"));
            domain2 = new DomainLoadContext("domain2", Path.Combine(pathBase, "domain2"));

            var asm1 = domain1.LoadFromAssemblyPath(Path.Combine(pathBase, "domain1", "Tests.dll"));
            var asm2 = domain2.LoadFromAssemblyPath(Path.Combine(pathBase, "domain2", "Tests.dll"));

            var type1 = asm1.GetType(typeof(InnerDriver).FullName);
            var type2 = asm2.GetType(typeof(InnerDriver).FullName);

            testsOnDomain1 = (IInnerDriver)Activator.CreateInstance(type1);
            testsOnDomain2 = (IInnerDriver)Activator.CreateInstance(type2);

            testsOnDomain1.Settings = settings;
            testsOnDomain2.Settings = settings;
        }

        public void DisposeDomains()
        {
            testsOnDomain1 = null;
            testsOnDomain2 = null;
            domain1.Unload();
            domain2.Unload();
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
                testsOnDomain2.DeserializeOnAppDomain(bytes, settings.GetSettings((ulong)(allowGuidChange ? vtl | VersionToleranceLevel.AllowGuidChange : vtl)));
                return true;
            }
            catch (Exception e)
            {
                // The exception will come from different domain, thus we will not have access to its type.
                // To check if it is a VersionTolerance exception we can compare FullNames which are equal across domains.
                if (e.GetType().FullName == typeof(VersionTolerance.VersionToleranceException).FullName)
                {
                    return false;
                }

                throw;
            }
        }

        protected IInnerDriver.DriverSettings settings;

        protected IInnerDriver testsOnDomain1;
        protected IInnerDriver testsOnDomain2;

        private AssemblyLoadContext domain1;
        private AssemblyLoadContext domain2;
    }

    public class InnerDriver : IInnerDriver
    {
        public InnerDriver()
        {
            DynamicType.Prefix = AppDomain.CurrentDomain.FriendlyName;
        }

        public void CreateInstanceOnAppDomain(DynamicType type, Version version = null)
        {
            obj = type.Instantiate(version);
        }

        public void DeserializeOnAppDomain(byte[] data, IInnerDriver.SettingsGenerator settingsGen)
        {
            var stream = new MemoryStream(data);
            var deserializer = new Serializer(CreateSettings(settingsGen));
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
            var type = FindClass(className);
            if (type == null)
            {
                throw new InvalidOperationException($"Could not find class '{className}'.");
            }

            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException($"Could not find field '{fieldName}' in a class '{className}'");
            }

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
            var serializer = new Serializer(CreateSettings(Settings.GetSettings()));
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

        public static Settings CreateSettings(IInnerDriver.SettingsGenerator settingsGenerator)
        {
            return new Settings(
                settingsGenerator.UseGeneratedSerializer ? Method.Generated : Method.Reflection,
                settingsGenerator.UseGeneratedDeserializer ? Method.Generated : Method.Reflection,
                (VersionToleranceLevel)settingsGenerator.Level,
                disableTypeStamping: !settingsGenerator.UseStamping);
        }

        public string Location { get; set; }
        public IInnerDriver.DriverSettings Settings { get; set; }

        protected object obj;
    }
}
