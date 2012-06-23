/*
  Copyright (c) 2012 Ant Micro <www.antmicro.com>

  Authors:
   * Konrad Kruczynski (kkruczynski@antmicro.com)
   * Piotr Zierhoffer (pzierhoffer@antmicro.com)

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
using System.Threading;
using NUnit.Framework;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;

namespace AntMicro.Migrant.Tests
{
	[TestFixture(false, false)]
	[TestFixture(true, false)]
	public class SerializationTests
	{

		public SerializationTests(bool useGeneratedSerializer, bool useGeneratedDeserializer)
		{
			this.useGeneratedDeserializer = useGeneratedDeserializer;
			this.useGeneratedSerializer = useGeneratedSerializer;
		}

		[Test]
		public void ShouldSerializeObject()
		{
			var obj = new object();
			var copy = SerializerClone(obj);
			Assert.AreEqual(copy.GetType(), typeof(object));
		}

		[Test]
		public void ShouldSerializeSimpleClass()
		{
			var simpleClass = new SimpleClass { Value = 0x69, Str = "Magic" };
			var copy = SerializerClone(simpleClass);
			Assert.AreEqual(simpleClass, copy);
		}

		[Test]
		public void ShouldSerializeCyclicReference()
		{
			var one = new CyclicRef();
			var another = new CyclicRef { Another = one, Val = 1 };
			one.Another = another;
			var oneCopy = SerializerClone(one);
			Assert.AreEqual(oneCopy, oneCopy.Another.Another, "Cyclic reference was not properly serialized.");
		}

		[Test]
		public void ShouldSerializeReferenceToSelf()
		{
			var one = new CyclicRef();
			one.Another = one;
			var copy = SerializerClone(one);
			Assert.AreSame(copy, copy.Another);
		}

		[Test]
		public void ShouldSerializeArrayReferenceToSelf()
		{
			var array = new object[2];
			array[0] = array;
			array[1] = array;
			var copy = SerializerClone(array);
			Assert.AreSame(copy, copy[0]);
			Assert.AreSame(copy[0], copy[1]);
		}

		[Test]
		public void ShouldSerializeNullReference()
		{
			var simpleClass = new SimpleClass();
			var copy = SerializerClone(simpleClass);
			Assert.AreEqual(simpleClass, copy);
		}

		[Test]
		public void ShouldPreserveIdentity()
		{
			var str = "This is a string";
			var simple1 = new SimpleClass { Str = str };
			var simple2 = new SimpleClass { Str = str };
			var container = new SimpleContainer { First = simple1, Second = simple2 };
			var copy = SerializerClone(container);
			Assert.AreSame(copy.First.Str, copy.Second.Str);
		}

		[Test]
		public void ShouldSerializeReadonlyField()
		{
			const int testValue = 0xABCD;
			var readonlyClass = new ClassWithReadonly(testValue);
			var copy = SerializerClone(readonlyClass);
			Assert.AreEqual(readonlyClass.ReadonlyValue, copy.ReadonlyValue);
		}

		[Test]
		public void ShouldNotSerializeIntPtr()
		{
			var intPtrClass = new ClassWithIntPtr{Ptr = new IntPtr(0x666)};
		    try
			{
				SerializerClone(intPtrClass);
				Assert.Fail("Class with IntPtr was serialized without exception.");
			}
			catch(ArgumentException)
			{

			}
			catch(InvalidOperationException)
			{

			}
		}

		[Test]
		public void ShouldSerializeWithInheritance()
		{
			SimpleBaseClass derived = new SimpleDerivedClass { BaseField = 1, DerivedField = 2 };
			var copy = (SimpleDerivedClass)SerializerClone(derived);
			Assert.AreEqual(copy.DerivedField, 2);
			Assert.AreEqual(copy.BaseField, 1);
		}

		[Test]
		public void ShouldSerializeBoxWithLazyScan()
		{
			var str = "Something";
			var box = new Box { Element = str };
			var copy = SerializerClone(box);
			Assert.AreEqual(str, copy.Element);
		}

		[Test]
		public void ShouldSerializeFieldWithDifferentFormalAndActualType()
		{
			var box = new Box();
			var anotherBox = new Box { Element = box };
			var copy = SerializerClone(anotherBox);
			Assert.AreEqual(typeof(Box), copy.Element.GetType());
		}

		[Test]
		public void ShouldSerializeListOfPrimitives()
		{
			var list = new List<int> { 1, 2, 3 };
			var copy = SerializerClone(list);
			CollectionAssert.AreEqual(list, copy);
		}

		[Test]
		public void ShouldSerializeLongListOfPrimitives()
		{
			var list = new List<int>();
			for(var i = 0; i < 100000; i++)
			{
				list.Add(i);
			}
			var copy = SerializerClone(list);
			CollectionAssert.AreEqual(list, copy);
		}

		[Test]
		public void ShouldSerializeListOfLists()
		{
			var first = new List<int> { 1, 2, 3 };
			var second = new List<int> { 97, 98, 99 };
			var list = new List<List<int>> { first, second };
			var copy = SerializerClone(list);
			for(var i = 0; i < 2; i++)
			{
				CollectionAssert.AreEqual(list[0], copy[0]);
			}
		}

		[Test]
		public void ShouldPreserveIdentityOnLists()
		{
			var list = new List<Box>();
			var box1 = new Box { Element = list };
			var box2 = new Box { Element = list };
			list.Add(box1);
			list.Add(box2);

			var copy = SerializerClone(list);
			Assert.AreSame(copy, copy[0].Element);
			Assert.AreSame(copy[0].Element, copy[1].Element);
		}

		[Test]
		public void ShouldSerializeArrayListWithStrings()
		{
			var list = new ArrayList { "Word 1", "Word 2", new SimpleClass { Value = 6, Str = "Word 4" }, "Word 3" };
			var copy = SerializerClone(list);
			CollectionAssert.AreEqual(list, copy);
		}

		[Test]
		public void ShouldSerializeArrayListWithPrimitives()
		{
			var list = new ArrayList { 1, 2, 3 };
			var copy = SerializerClone(list);
			CollectionAssert.AreEqual(list, copy);
		}

		[Test]
		public void ShouldSerializeHashSetWithPrimitives()
		{
			var collection = new HashSet<int> { 1, 2, 3 };
			var copy = SerializerClone(collection);
			CollectionAssert.AreEquivalent(collection, copy);
		}

		[Test]
		public void ShouldSerializeBlockingCollectionWithPrimitives()
		{
			var collection = new BlockingCollection<int> { 1, 2, 3 };
			var copy = SerializerClone(collection);
			CollectionAssert.AreEquivalent(collection, copy);
		}

		[Test]
		public void ShouldSerializeBoxedPrimitve()
		{
			var box = new Box { Element = 3 };
			var copy = SerializerClone(box);
			Assert.AreEqual(3, copy.Element);
		}

		[Test]
		public void ShouldPreserveIdentityWithBoxedPrimitives()
		{
			object primitive = 1234;
			var box1 = new Box { Element = primitive };
			var box2 = new Box { Element = primitive };
			var list = new List<Box> { box1, box2 };
			var copy = SerializerClone(list);
			Assert.AreSame(copy[0].Element, copy[1].Element);
		}

		[Test]
		public void ShouldSerializeCollectionWithStrings()
		{
			var collection = new BlockingCollection<string> { "One", "Two", "Three" };
			var copy = SerializerClone(collection);
			CollectionAssert.AreEquivalent(collection, copy);
		}

		[Test]
		public void ShouldSerializeListWithNull()
		{
			var list = new List<object> { "One", null, "Two" };
			var copy = SerializerClone(list);
			CollectionAssert.AreEqual(list, copy);
		}

		//public void ShouldSerialize

		[Test]
		public void ShouldSerializeCollectionWithNull()
		{
			var collection = new BlockingCollection<object> { 1, null, 3 };
			var copy = SerializerClone(collection);
			CollectionAssert.AreEquivalent(collection, copy);
		}

		[Test]
		public void ShouldSerializeArrayWithPrimitives()
		{
			var array = new [] { 1, 2, 3, 4, 5, 6 };
			var copy = SerializerClone(array);
			CollectionAssert.AreEqual(array, copy);
		}

		[Test]
		public void ShouldSerializeTwodimensionalArrayWithPrimitives()
		{
			var array = new [,] { { 1, 2 }, { 3, 4 }, { 5, 6 } };
			var copy = SerializerClone(array);
			CollectionAssert.AreEqual(array, copy);
		}

		[Test]
		public void ShouldSerializeArrayWithStrings()
		{
			var array = new [] { "One", "Two", "Three" };
			var copy = SerializerClone(array);
			CollectionAssert.AreEqual(array, copy);
		}

		[Test]
		public void ShouldSerializeTwodimensionalArrayWithStrings()
		{
			var array = new [,] { { "One", "Two" }, { "Three", "Four" } };
			var copy = SerializerClone(array);
			CollectionAssert.AreEqual(array, copy);
		}

		[Test]
		public void ShouldSerialize4DArrayWithPrimitives()
		{
			var array = new int[2,2,3,4];
			array[0,0,0,0] = 1;
			array[1,0,1,0] = 2;
			array[0,1,0,1] = 3;
			array[0,1,2,2] = 4;
			array[1,0,2,3] = 5;
			var copy = SerializerClone(array);
			Assert.AreEqual(array, copy);
		}

		[Test]
		public void ShouldSerializeMixedArray()
		{
			var obj = new object();
			var array = new [] { "One", obj, "Two", obj, new object() };
			var copy = SerializerClone(array);
			CollectionAssert.AreEqual(array.Where(x => x is String), copy.Where(x => x is String));
			Assert.AreSame(copy[1], copy[3]);
			Assert.AreNotSame(copy[3], copy[4]);
		}

		[Test]
		public void ShouldSerializeDictionaryWithPrimitives()
		{
			var dictionary = new Dictionary<int, int>();
			for(var i = 0; i < 100; i++)
			{
				dictionary.Add(i, i + 1);
			}
			var copy = SerializerClone(dictionary);
			CollectionAssert.AreEquivalent(dictionary, copy);
		}

		[Test]
		public void ShouldSerializeHashtable()
		{
			var hashtable = new Hashtable();
			for(var i = 0; i < 100; i++)
			{
				hashtable.Add(i, i + 1);
			}
			var copy = SerializerClone(hashtable);
			CollectionAssert.AreEquivalent(hashtable, copy);
		}

		[Test]
		public void ShouldSerializeDictionaryWithStrings()
		{
			var dictionary = new Dictionary<string, string>
			{
				{ "Ball", "Piłka" },
				{ "Cat", "Kot" },
				{ "Line", "Linia" }
			};
			var copy = SerializerClone(dictionary);
			CollectionAssert.AreEquivalent(dictionary, copy);
		}

		[Test]
		public void ShouldPreserveIdentityWithDictionary()
		{
			var str = "Word";
			var dictionary = new Dictionary<int, string>
			{
				{ 10, str },
				{ 20, "One" },
				{ 30, str }
			};
			var copy = SerializerClone(dictionary);
			CollectionAssert.AreEquivalent(dictionary, copy);
			Assert.AreSame(copy[10], copy[30]);
			Assert.AreNotSame(copy[10], copy[20]);
		}

		[Test]
		public void ShouldSerializeSimpleEnum()
		{
			var e = SimpleEnum.Two;
			var copy = SerializerClone(e);
			Assert.AreEqual(e, copy);
		}

		[Test]
		public void ShouldSerializeEnums()
		{
			var enumLongValues = Enum.GetValues(typeof(TestEnumLong)).Cast<TestEnumLong>().ToArray();
			var enumShortValues = Enum.GetValues(typeof(TestEnumShort)).Cast<TestEnumShort>().ToArray();
			var enumFlagsValues = Enum.GetValues(typeof(TestEnumFlags)).Cast<TestEnumFlags>()
				.Union(new []
				{
					TestEnumFlags.First | TestEnumFlags.Second
					,TestEnumFlags.First | TestEnumFlags.Second | TestEnumFlags.Third
					, TestEnumFlags.Second | TestEnumFlags.Third
				}).ToArray();

			foreach (var item in enumLongValues) 
			{
				Assert.AreEqual(item, SerializerClone(item));
			}
			foreach (var item in enumShortValues) 
			{
				Assert.AreEqual(item, SerializerClone(item));
			}
			foreach (var item in enumFlagsValues) 
			{
				Assert.AreEqual(item, SerializerClone(item));
			}
		}

		[Test]
		public void ShouldSerializeSimplerStruct()
		{
			var str = new SimplerStruct { A = 1234567, B = 543 };
			var copy = SerializerClone(str);
			Assert.AreEqual(str, copy);
		}

		[Test]
		public void ShouldSerializeSimpleStruct()
		{
			var str = new SimpleStruct
			{
				A = 5,
				B = "allman"
			};
			var copy = SerializerClone(str);
			Assert.AreEqual(str, copy);
		}

		[Test]
		public void ShouldSerializeComplexStruct()
		{
			var str = new SimpleStruct
			{
				A = 5,
				B = "allman"
			};

			var compstr = new ComplexStruct
			{
				A = 6,
				B = str
			};
			var newStr = SerializerClone(compstr);

			Assert.AreEqual(compstr, newStr);
		}

		[Test]
		public void ShouldSerializeStructInClass()
		{
			var str = new SimpleStruct { A = 1, B = "allman" };
			var box = new GenericBox<SimpleStruct> { Element = str };
			var copy = SerializerClone(box);
			Assert.AreEqual(str, copy.Element);
		}

		[Test]
		public void ShouldSerializeSpeciallySerializable()
		{
			var special = new SpeciallySerializable();
			special.Fill(100);
			var copy = SerializerClone(special);
			CollectionAssert.AreEqual(special.Data, copy.Data);
		}

		[Test]
		public void ShouldSerializeSpeciallySerializableInArrayAndPreserveIdentity()
		{
			var special = new SpeciallySerializable();
			special.Fill(100);
			var str = "Some string";
			var box = new Box { Element = special };
			var anotherSpecial = new SpeciallySerializable();
			anotherSpecial.Fill(100);
			var array = new object[] { special, str, box, anotherSpecial };
			var copy = SerializerClone(array);
			Assert.AreSame(copy[0], ((Box)copy[2]).Element);
			Assert.AreNotSame(copy[0], copy[3]);
			CollectionAssert.AreEqual(((SpeciallySerializable)copy[0]).Data, special.Data);
			CollectionAssert.AreEqual(((SpeciallySerializable)copy[3]).Data, anotherSpecial.Data);
		}

		[Test]
		public void ShouldSerializeBoxWithLong()
		{
			var box = new GenericBox<long> { Element = 1234 };
			var copy = SerializerClone(box);
			Assert.AreEqual(box.Element, copy.Element);
		}

		[Test]
		public void ShouldSerializeStructWithSpeciallySerializable()
		{
			var special = new SpeciallySerializable();
			special.Fill(100);
			var box = new StructGenericBox<SpeciallySerializable> { Element = special };
			var copy = SerializerClone(box);
			Assert.AreEqual(box.Element.Data, copy.Element.Data);
		}

		[Test]
		public void ShouldSerializeNonNullNullable()
		{
			var box = new GenericBox<int?> { Element = 3 };
			var copy = SerializerClone(box);
			Assert.AreEqual(box.Element, copy.Element);
		}

		[Test]
		public void ShouldSerializeNullNullable()
		{
			var box = new GenericBox<int?> { Element = null };
			var copy = SerializerClone(box);
			Assert.AreEqual(box.Element, copy.Element);
		}

		[Test]
		public void ShouldSerializeBoxedNullNullable()
		{
			int? value = 66;
			var box = new Box { Element = value };
			var copy = SerializerClone(box);
			Assert.AreEqual(box.Element, copy.Element);
		}

		[Test]
		public void ShouldSerializeBoxedNonNullNullable()
		{
			int? value = null;
			var box = new Box { Element = value };
			var copy = SerializerClone(box);
			Assert.AreEqual(box.Element, copy.Element);
		}

		[Test]
		public void ShouldSerializeConcurrentDictionary()
		{
			var dictionary = new ConcurrentDictionary<int, int>();
			for(var i = 0; i < 10; i++)
			{
				dictionary.TryAdd(i, 2*i);
			}
			var copy = SerializerClone(dictionary);
			CollectionAssert.AreEquivalent(dictionary, copy);
		}

		[Test]
		public void ShouldHandleTwoSerializations()
		{
			var someObjects = new object[] { "One", 2, null, "Four" };
			var stream = new MemoryStream();
			var serializer = new Serializer(SettingsFromFields);
			serializer.Serialize(someObjects, stream);
			serializer.Serialize(someObjects, stream);
			stream.Seek(0, SeekOrigin.Begin);
			var copy = serializer.Deserialize<object[]>(stream);
			CollectionAssert.AreEqual(someObjects, copy);
			copy = serializer.Deserialize<object[]>(stream);
			CollectionAssert.AreEqual(someObjects, copy);
		}

		[Test]
		public void ShouldDetectStreamShift()
		{
			var errorneousObject = new BadlySerializable();
			try
			{
				SerializerClone(errorneousObject);
				Assert.Fail("Exception was not thrown despite stream corruption.");
			}
			catch(InvalidOperationException)
			{
			}
		}

		[Test]
		public void ShouldSerializeTuple()
		{
			var tuple = Tuple.Create("One", "Two", "Three");
			var copy = SerializerClone(tuple);
			Assert.AreEqual(tuple, copy);
		}

		[Test]
		public void ShouldInvokeConstructorForField()
		{
			var withCtor = new WithConstructorAttribute();
			var copy = SerializerClone(withCtor);
			Assert.IsTrue(copy.IsFieldConstructed, "[Constructor] marked field was not initialized.");
		}

		[Test]
		public void ShouldOmitTransientClass()
		{
			SerializerClone(new Box { Element = new TransientClass() });
		}

		[Test]
		public void ShouldSerializeQueue()
		{
			var queue = new Queue<int>();
			queue.Enqueue(1);
			queue.Enqueue(3);
			queue.Enqueue(5);
			var copy = SerializerClone(queue);
			CollectionAssert.AreEqual(queue, copy);
		}

		[Test]
		public void ShouldSerializeWithInitialization()
		{
			var objs = new object[] { null, 1,"Napis", new GenericBox<int> { Element = 6 } };
			var serializer = new Serializer(SettingsFromFields);
			serializer.Initialize(typeof(GenericBox<int>));
			serializer.Initialize(typeof(string));
			var stream = new MemoryStream();
			serializer.Serialize(objs, stream);
			stream.Seek(0, SeekOrigin.Begin);
			var copy = serializer.Deserialize<object[]>(stream);
			CollectionAssert.AreEqual(objs, copy);
		}

		[Test]
		public void ShouldSerializeByteArray()
		{
			var array = new byte[] { 2, 4, 6, 8, 10 };
			var copy = SerializerClone(array);
			CollectionAssert.AreEqual(array, copy);
		}

		private T SerializerClone<T>(T toClone)
		{
			var settings = SettingsFromFields;
			return Serializer.DeepClone(toClone, settings);
		}

		private Customization.Settings SettingsFromFields
		{
			get
			{
				var settings = new Customization.Settings
				(
					useGeneratedSerializer ? Customization.Method.Generated : Customization.Method.Reflection,
					useGeneratedDeserializer ? Customization.Method.Generated : Customization.Method.Reflection
				);
				return settings;
			}
		}

		private bool useGeneratedSerializer;
		private bool useGeneratedDeserializer;

		public class SimpleClass
		{
			public int Value { get; set; }
			public string Str { get; set; }

			public override bool Equals(object obj)
			{
				if(obj == null)
				{
					return false;
				}
				if(ReferenceEquals(this, obj))
				{
					return true;
				}
				if(obj.GetType() != typeof(SimpleClass))
				{
					return false;
				}
				var other = (SimpleClass)obj;
				return Value == other.Value && Str == other.Str;
			}			

			public override int GetHashCode()
			{
				unchecked
				{
					return Value.GetHashCode() ^ 33*(Str != null ? Str.GetHashCode() : 0);
				}
			}

			public override string ToString()
			{
				return string.Format("[SimpleClass: Value={0}, Str={1}]", Value, Str);
			}			
		}

		public class SimpleContainer
		{
			public SimpleClass First { get; set; }
			public SimpleClass Second { get; set; }
		}

		public class CyclicRef
		{
			public CyclicRef Another { get; set; }
			public int Val { get; set; }
		}

		public class ClassWithReadonly
		{
			public ClassWithReadonly(int readonlyValue)
			{
				privateReadonlyField = readonlyValue;
			}

			public int ReadonlyValue
			{
				get
				{
					return privateReadonlyField;
				}
			}

			private readonly int privateReadonlyField;
		}

		public class ClassWithIntPtr
		{
			public IntPtr Ptr { get; set; }
		}

		public class SimpleBaseClass
		{
			public int BaseField { get; set; }
		}

		public class SimpleDerivedClass : SimpleBaseClass
		{
			public int DerivedField { get; set; }
		}

		public class Box
		{
			public object Element { get; set; }
		}

		public sealed class GenericBox<T>
		{
			public T Element { get; set; }

			public override bool Equals(object obj)
			{
				if(obj == null)
				{
					return false;
				}
				if(ReferenceEquals(this, obj))
				{
					return true;
				}
				if(obj.GetType() != typeof(GenericBox<T>))
				{
					return false;
				}
				var other = (GenericBox<T>)obj;
				if(Element != null)
				{
					return Element.Equals(other.Element);
				}
				return other.Element == null;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (Element != null ? Element.GetHashCode() : 0);
				}
			}
		}

		public struct StructGenericBox<T>
		{
			public T Element;
		}

		public enum SimpleEnum
		{
			One,
			Two,
			Three
		}

		private enum TestEnumLong : long
		{
			First = -1,
			Second = long.MaxValue-12345,
			Third
		}
		
		private enum TestEnumShort : short
		{
			First = short.MinValue+2,
			Second = 6,
			Third
		}

		[Flags]
		private enum TestEnumFlags
		{
			First = 1,
			Second = 2,
			Third = 4
		}

		private struct SimplerStruct
		{
			public int A;
			public long B;

			public override string ToString()
			{
				return string.Format("[SimplerStruct: A={0}, B={1}]", A, B);
			}
		}

		private struct SimpleStruct
		{
			public int A;
			public String B;

			public override string ToString()
			{
				return string.Format("[SimpleStruct: A={0}, B={1}]", A, B);
			}
		}

		private struct ComplexStruct
		{
			public int A;
			public SimpleStruct B;

			public override string ToString()
			{
				return string.Format("[ComplexStruct: A={0}, B={1}]", A, B);
			}
		}

		private class SpeciallySerializable : ISpeciallySerializable
		{
			public void Fill(int length)
			{
				var bytes = new byte[length];
				Helpers.Random.NextBytes(bytes);
				data = bytes.Select(x => new IntPtr(x)).ToArray();
			}

			public byte[] Data
			{
				get
				{
					return data.Select(x => (byte)x).ToArray();
				}
			}

			public void Load(PrimitiveReader reader)
			{
				var length = reader.ReadInt32();
				data = new IntPtr[length];
				for(var i = 0; i < length; i++)
				{
					data[i] = new IntPtr(reader.ReadByte());
				}
			}

			public void Save(PrimitiveWriter writer)
			{
				writer.Write(data.Length);
				for(var i = 0; i < data.Length; i++)
				{
					writer.Write((byte)data[i]);
				}
			}

			private IntPtr[] data;
		}

		private class BadlySerializable : ISpeciallySerializable
		{
			public void Load(PrimitiveReader reader)
			{
				reader.ReadInt32();
				reader.ReadInt32();
			}

			public void Save(PrimitiveWriter writer)
			{
				for(var i = 0; i < 3; i++)
				{
					writer.Write(666 + i);
				}
			}
		}

		private class WithConstructorAttribute
		{
			public bool IsFieldConstructed
			{
				get
				{
					return resetEvent != null;
				}
			}

			[Constructor(false)]
			private ManualResetEvent resetEvent;
		}

		[Transient]
		private class TransientClass
		{
			public IntPtr Pointer { get; set; }
		}
	}
}

