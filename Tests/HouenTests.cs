using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AntMicro.Migrant;
using AntMicro.Migrant.Utilities;
using Migrant.Generators;
using NUnit.Framework;
using AntMicro.Migrant.Customization;
using System.Diagnostics;

namespace Tests
{
    public class HouenTests
    {
		private class Foo
		{
		    public char bar = (char)7;
		    //public int x = 453;
		    //public float y = 3.5f;
		    //public string z;
			public Foo ss;
		}

		[Test]
		public void SimpleTest()
		{
		    var preser = new Foo();
		    //preser.bar = 133;
		    //preser.x = 53654645;
		    //preser.y = 3232.432432f;
		    //preser.z = "Asddas";
			preser.ss = new Foo();

		    //var x = "Asdas yeah";
		    var x = 1234232432;

		    var mstream = new MemoryStream();
			var ser = new Serializer(new Settings(Method.Reflection, Method.Generated));
			ser.Serialize(preser, mstream);
		    mstream.Seek(0, SeekOrigin.Begin);

			using (var file = File.OpenWrite("streamed4.hex"))
			{
				byte[] buffer = new byte[8 * 1024];
				int len;
				while ( (len = mstream.Read(buffer, 0, buffer.Length)) > 0)
				{
					file.Write(buffer, 0, len);
				}    
			}
			/*
            var foo = ser.Deserialize<Foo>(mstream);

			//Assert.AreEqual(preser, foo);
			//Assert.AreEqual(preser.bar, foo.bar);
    	    //Assert.AreEqual(preser.x, foo.x);
			//Assert.AreEqual(preser.y, foo.y);
			Assert.AreEqual(preser.ss, preser);
			Assert.AreEqual(foo.ss, foo);
			//Debug.Print(preser.str);
			//Debug.Print(foo.str);

            //Assert.AreEqual(preser.z, foo.z);
            */
		}
    }
}
