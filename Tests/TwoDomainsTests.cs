// *******************************************************************
//
//  Copyright (c) 2011-2014, Antmicro Ltd <antmicro.com>
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
using System;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using TestsSecondModule;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Antmicro.Migrant.Tests
{
    public class TwoDomainsTests
    {
        public TwoDomainsTests()
        {
        }

        [Test]
        public void ShouldSerializeClassWithOverride()
        {
            RunInTwoDomains(
@"
    public class BaseClassForOverriding
    {
        public int P;
    }

    public class DerivedClassForOverriding : BaseClassForOverriding
    {
        public new int P;
    }
",
            (f) =>
            {
                var s = new Serializer();
                var obj = (object)Activator.CreateInstance(f("DerivedClassForOverriding"));
                using(var fs = new FileStream("serialized", FileMode.OpenOrCreate))
                {
                    s.Serialize(obj, fs);
                }
            },
            (f) =>
            {
                var s = new Serializer();
                using(var fs = File.OpenRead("serialized"))
                {
                var method = typeof(Serializer).GetMethod("Deserialize").MakeGenericMethod(f("DerivedClassForOverriding"));
                    method.Invoke(s, new [] { fs });
                }
            });
        }

        [Test]
        public void ShouldDetectChangeOfBaseClass()
        {
            RunInTwoDomains(
@"
    public class A
    {
    }

    public class B : A
    {
        public int i;
    }
",
            (f) => 
            {
                var s = new Serializer();
                var obj = (object)Activator.CreateInstance(f("B"));
                using(var fs = new FileStream("serialized", FileMode.OpenOrCreate))
                {
                    s.Serialize(obj, fs);
                }
            },
@"
    public class C
    {
    }

    public class B : C
    {
        public int i;
    }
",
            (f) => 
            {
                var s = new Serializer();
                using(var fs = File.OpenRead("serialized"))
                {
                    var method = typeof(Serializer).GetMethod("Deserialize").MakeGenericMethod(f("B"));
                    try 
                    {
                        method.Invoke(s, new [] { fs });
                        Assert.Fail();
                    } 
                    catch (TargetInvocationException ex)
                    {
                        if (!(ex.InnerException is InvalidOperationException))
                        {
                            Assert.Fail();
                        }
                    }
                }
            });
        }

        [Test]
        public void ShouldDetectMovementOfFieldBetweenClass()
        {
            RunInTwoDomains(
@"
    public class A
    {
        public int a;
    }

    public class B : A
    {
    }
",
            (f) => 
            {
                var s = new Serializer();
                var obj = (object)Activator.CreateInstance(f("B"));
                using(var fs = new FileStream("serialized", FileMode.OpenOrCreate))
                {
                    s.Serialize(obj, fs);
                }
            },
@"
    public class A
    {
    }

    public class B : A
    {
        public int a;
    }
",
            (f) => 
            {
                var s = new Serializer();
                using(var fs = File.OpenRead("serialized"))
                {
                    var method = typeof(Serializer).GetMethod("Deserialize").MakeGenericMethod(f("B"));
                    try 
                    {
                        method.Invoke(s, new [] { fs });
                        Assert.Fail();
                    } 
                    catch (TargetInvocationException ex)
                    {
                        if (!(ex.InnerException is InvalidOperationException))
                        {
                            Assert.Fail();
                        }
                    }
                }
            } );
        }

        [Test]
        public void ShouldDetectInsertionOfBaseClass()
        {
            RunInTwoDomains(
@"
    public class B 
    {
        public int a;
    }
",
            (f) => 
            {
                var s = new Serializer();
                var obj = (object)Activator.CreateInstance(f("B"));
                using(var fs = new FileStream("serialized", FileMode.OpenOrCreate))
                {
                    s.Serialize(obj, fs);
                }
            },
@"
    public class A
    {
        public int a;
    }

    public class B : A
    {
        public int b;
    }
",
            (f) => 
            {
                var s = new Serializer();
                using(var fs = File.OpenRead("serialized"))
                {
                    var method = typeof(Serializer).GetMethod("Deserialize").MakeGenericMethod(f("B"));
                    try 
                    {
                        method.Invoke(s, new [] { fs });
                        Assert.Fail();
                    } 
                    catch (TargetInvocationException ex)
                    {
                        if (!(ex.InnerException is InvalidOperationException))
                        {
                            Assert.Fail();
                        }
                    }
                }
            } );
        }

        private void RunInTwoDomains(string code_one, Action<Func<string, Type>> action_one, string code_two, Action<Func<string, Type>> action_two)
        {
            var dlls = new [] {
                Assembly.GetAssembly(typeof(Serializer)).Location,
                Assembly.GetAssembly(typeof(ISerializationDriverInterface)).Location
            };

            var code = new Dictionary<string, string> { {"one", code_one}, {"two", code_two } };
            var action = new Dictionary<string, Action<Func<string, Type>>> { {"one", action_one}, {"two", action_two} };

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var dir in new string[] { "one", "two" })
            {
                Directory.CreateDirectory(Path.Combine(baseDir, dir));
                File.Copy (Path.Combine (baseDir, "Migrant.dll"), Path.Combine (baseDir, dir, "Migrant.dll"), true);
                File.Copy (Path.Combine (baseDir, "Tests.dll"), Path.Combine (baseDir, dir, "Tests.dll"), true);
                File.Copy (Path.Combine (baseDir, "TestsSecondModule.dll"), Path.Combine (baseDir, dir, "TestsSecondModule.dll"), true);
                File.Copy (Path.Combine (baseDir, "nunit.framework.dll"), Path.Combine (baseDir, dir, "nunit.framework.dll"), true);

                AdHocCompiler.CompileSource(CODE.Replace("{REPLACE}", code[dir]), Path.Combine(dir,"dcc.dll"), dlls);

                var domain = AppDomain.CreateDomain("domain " + dir, null, new AppDomainSetup() { ApplicationBase = dir });
                var driver = (ISerializationDriverInterface)domain.CreateInstanceFromAndUnwrap(Path.Combine(baseDir, dir, "dcc.dll"), "Antmicro.Migrant.Tests.TestDriver");

                driver.RunAction(action[dir]);
            }
        }

        private void RunInTwoDomains(string code, Action<Func<string, Type>> action_one, Action<Func<string, Type>> action_two)
        {
            RunInTwoDomains(code, action_one, code, action_two);
        }

        private static string CODE = 
@"using System;
using System.IO;

namespace Antmicro.Migrant.Tests
{
    {REPLACE}

    public class TestDriver : MarshalByRefObject, TestsSecondModule.ISerializationDriverInterface
    {
        public void RunAction(Action<Func<string, Type>> a)
        {
            a(s => Type.GetType(string.Format(""Antmicro.Migrant.Tests.{0}, dcc, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"", s)));
        }
    }
}";
    }
}
