# Migrant 0.14

[![Coverity Scan Build Status](https://scan.coverity.com/projects/3674/badge.svg)](https://scan.coverity.com/projects/3674)

This is the *Migrant* project by [Antmicro](http://antmicro.com), a fast and flexible serialization framework usable for undecorated classes, written in C\#.

## Table of Contents

1.  Introduction
1.  Directory organization
1.  Usage
1.  Features
1.  Download
1.  Compilation
1.  More information
1.  Licence
1.  Authors

## Introduction

Migrant is a serialization framework for .NET and Mono projects. Its aim is to provide an easy way to serialize complex graphs of objects, with minimal programming effort.

## Directory organization

There are three main directories:

- ``Migrant`` - contains the source code of the library;
- ``Tests`` - contains unit tests (we're using NUnit);
- ``PerformanceTester`` - contains performance assessment project;

There are two solution files - Migrant.sln, the core library, and MigrantWithTests.sln, combining both tests project and Migrant library.

## Usage

Here we present some simple use cases of Migrant. They are written in pseudo-C\#, but can be easily translated to other CLI languages.

### Simple serialization

    var stream = new MyCustomStream();
    var myComplexObject = new MyComplexType(complexParameters);
    var serializer = new Serializer();

    serializer.Serialize(myComplexObject, stream);

    stream.Rewind();

    var myDeserializedObject = serializer.Deserialize<MyComplexType>(stream);

### Open stream serialization

Normally each serialization data contains everything necessary for serialization like type descriptions. Those data, however, consume space and sometimes one would like to do consecutive serializations where each next one would reuse context just as if they happen as one. In other words, serializing one object and then another would be like serializing tuple of them. This also means that if there is a reference to an old object, it will be used instead of a new object serialized second time. Given open stream serialization session is tied to the stream. API is simple:

    var serializer = new Serializer();
    using(var osSerializer = serializer.ObtainOpenStreamSerializer(stream))
    {
        osSerializer.Serialize(firstObject);
        osSerializer.Serialize(secondObject);
    }

Here's deserialization:

    var serializer = new Serializer();
    using(var osSerializer = serializer.ObtainOpenStreamDeserializer(stream))
    {
        var firstObject = osSerializer.Deserialize<MyObject>();
        var secondObject = osSerializer.Deserialize<MyObject>();
    }

But what if you'd like to deserialize all object of a given type until the end of stream is reached? Here's the solution:

    osDeserializer.DeserializeMany<T>()

which returns lazy `IEnumerable<T>`.

As default, all writes to the stream are buffered, i.e. one can be only sure that they are in the stream after calling `Dispose()` on open stream (de)serializer. This is, however, not useful when underlying stream is buffered independently or it is a network stream attached to a socket. In such cases one can disable buffering in `Settings`. Note that buffering also disables padding which is normally used to allow speculative reads. Therefore data written with buffered and unbuffered mode are not compatible.

#### Reference preservation

Let's assume you do open stream serialization. In first session, some object, let's name it A, is serialized. In the second session, among others, A is serialized again - the same exact instance. What should happen? We prefer when reference is preserved, so that when A is serialized in second session, actually only its id goes to the second session - pointing to the object from the first session. However, to provide such feature we have to keep reference to A between sessions - to check whether it is the same instance as previously serialized. This means that until open stream serializer is disposed, a reference to A is held, hence it won't be collected by GC. To prevent this unpleasant situation we could use weak references. Unfortunately, as it turned out, with frequent serialization of small objects this can completely kill Migrant's performance - which is a core value in our library. So, to sum it up we offer user to choose from three mentioned options with the help of `Settings` class. The possible values of an enum `ReferencePreservation` are:
- `DoNotPreserve` - each session is treated as separate serialization considering references;
- `Preserve` - object identity is preserved but they are strongly referenced between sessions;
- `UseWeakReference` - best of both worlds conceptually, but can completely kill your performance.

Choose wisely.

### Deep clone

    var myComplexObject = new MyComplexType(complexParameters);
    var myObjectCopy = Serializer.DeepCopy(myComplexObject);

### Simple types to bytes

    var myLongArray = new long[] { 1, 2, ... };
    var myOtherArray = new long[myLongArray.Length];
    var stream = new MyCustomStream();

    using(var writer = new PrimitiveWriter(stream))
    {
       foreach(var element in myLongArray)
       {
          writer.Write(element);
       }
    }

    stream.Rewind();

    using(var reader = new PrimitiveReader(stream))
    {
       for(var i=0; i<myLongArray.Length; i++)
       {
          myOtherArray[i] = reader.ReadInt64();
       }
    }

### Surrogates

    var serializer = new Serializer();
    var someObject = new SomeObject();
    serializer.ForObject<SomeObject>().SetSurrogate(x => new AnotherObject());
    serializer.Serialize(someObject, stream);

    stream.Rewind();

    var anObject = serializer.Deserialize<object>(stream);
    Console.WriteLine(anObject.GetType().Name); // prints AnotherObject

One can also use a `Type` based API, i.e.

```csharp
serializer.ForObject(typeof(SomeObject)).SetSurrogate(x => new AnotherObject());
```

What's the usage? The *generic surrogates*, which can match to many concrete types. Such surrogates are defined on open generic types.
Here is an example:
```csharp
serializer.ForObject(typeof(Tuple<>)).SetSurrogate(x => x.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)[0].GetValue(x));
```

### Version tolerance

What if some changes are made to the layout of the class between serialization and deserialization? Migrant can cope with that up to some extent. During creation of serializer you can specify settings, among which there is a version tolerance level. In the most restrictive (default) configuration, deserialization is possible if module ID (which is GUID generated when module is compiled) is the same as it was during serialization. In other words, serialization and deserialization must be done with the same assembly.

However, there is a way to weaken this condition by specifing flags from a special enumeration called `VersionToleranceLevel`:

- ``AllowGuidChange`` - GUID values may differ between types which means that it can come from different compilations of the same library. This option alone effectively means that the layout of the class (fields, base class, name) does not change.
- ``AllowFieldAddition`` - new version of the type can contain more fields than it contained during serialization. They are initialized with their default values.
- ``AllowFieldRemoval`` - new version of the type can contain less fields than it contained during serialization.
- ``AllowInheritanceChainChange`` - inheritance chain can change, i.e. base class can be added/removed.
- ``AllowAssemblyVersionChange`` - assembly version (but not it's name or culture) can differ between serialized and deserializad types.

As GUID numbers may vary between compilations of the same code, it is obvious that more significant changes will also cause them to change. To simplify the API, ``AllowGuidChange`` value is implied by any other option.

### Collections handling

By default Migrant tries to handle standard collection types serialization in a special
way. Instead of writing to the stream all of private meta fields describing collection
object, only items are serialized - in a similar way to arrays. During
deserialization a collection is recreated by calling proper adders methods.

This approach limits stream size and allows to easily migrate between versions
of .NET framework, as internal collection implementation may differ between them.

Described mechanisms works for the following collections:

- ``List<>``
- ``ReadOnlyCollection<>``
- ``Dictionary<,>``
- ``HashSet<>``
- ``Queue<>``
- ``Stack<>``
- ``BlockingCollection<>``
- ``Hashtable``

There is, however, an option to disable this feature and treat collections as
normal user objects. To do this a flag ``treatCollectionAsUserObject`` in the ``Settings`` object must be set to ``true``.

### `ISerializable` support

By default Migrant does not use special serialization means for classes or
structs that implement `ISerializable`. If you have already prepared your code
for e.g. `BinaryFormatter`, then you can turn on `ISerializable` support in
`Settings`. Note that this is suboptimal compared to normal Migrant's approach
and is only thought as a compatibility layer which may be useful for the
plug-in serialization framework substitution. Also note that it will also
affect framework classes, resulting in a suboptimal performance. For example:

  `Dictionary`, one million elements.
  Without ISerializable support: 0.30s,  13.25MB
  With ISeriazilable support:    6.94s, 13.25MB

  `Dictionary`, 10000 instances with one element each.
  Without ISerializable support: 44.79ms, 184.01KB
  With ISeriazilable support:    0.91s,   6.36MB

To sum it up, such a support is meant to be phased out during time in your
project.

### `IXmlSerializable` support

As with `ISerializable` Migrant can also utilize implementation of
`IXmlSerializable`. In that case data is written to memory stream using
`XmlSerializer` and then taken as a binary blob (along with type name).

### (De)serializing the `Type` type

Directly serializing `Type` is not possible, but you can use surrogates for that purpose.

One solution is to serialize the assembly qualified name of the type:

```csharp

class MainClass
{
	public static void Main(string[] args)
	{
		var serializer = new Serializer();
		serializer.ForObject<Type>().SetSurrogate(type => new TypeSurrogate(type));
		serializer.ForSurrogate<TypeSurrogate>().SetObject(x => x.Restore());

		var typesIn = new [] { typeof(Type), typeof(int[]), typeof(MainClass) };

		var stream = new MemoryStream();
		serializer.Serialize(typesIn, stream);
		stream.Seek(0, SeekOrigin.Begin);
		var typesOut = serializer.Deserialize<Type[]>(stream);
		foreach(var type in typesOut)
		{
			Console.WriteLine(type);
		}
	}
}

public class TypeSurrogate
{
	public TypeSurrogate(Type type)
	{
		assemblyQualifiedName = type.AssemblyQualifiedName;
	}

	public Type Restore()
	{
		return Type.GetType(assemblyQualifiedName);
	}

	private readonly string assemblyQualifiedName;
}
```

If you would also like to use the same mechanisms of version tolerance that are used for normal types, you can go with this code:

```csharp
class MainClass
{
	public static void Main(string[] args)
	{
		var serializer = new Serializer();
		serializer.ForObject<Type>().SetSurrogate(type => Activator.CreateInstance(typeof(TypeSurrogate<>).MakeGenericType(new [] { type })));
		serializer.ForSurrogate<ITypeSurrogate>().SetObject(x => x.Restore());

		var typesIn = new [] { typeof(Type), typeof(int[]), typeof(MainClass) };

		var stream = new MemoryStream();
		serializer.Serialize(typesIn, stream);
		stream.Seek(0, SeekOrigin.Begin);
		var typesOut = serializer.Deserialize<Type[]>(stream);
		foreach(var type in typesOut)
		{
			Console.WriteLine(type);
		}
	}
}

public class TypeSurrogate<T> : ITypeSurrogate
{
	public Type Restore()
	{
		return typeof(T);
	}
}

public interface ITypeSurrogate
{
	Type Restore();
}
```

## Features

Migrant is designed to be easy to use. For most cases, the scenario consists of calling one method to serialize, and another to deserialize a whole set of interconnected objects. It's not necessary to provide any information about serialized types, only the root object to save. All of the other objects referenced by the root are serialized automatically. It works out of the box for value and reference types, complex collections etc. While serialization of certain objects (e.g. pointers) is meaningless and may lead to hard-to-trace problems, Migrant will gracefully fail to serialize such objects, providing the programmer with full information on what caused the problem and where is it located.

The output of the serialization process is a stream of bytes, intended to reflect the memory organization of the actual system. This data format is compact and thus easy to transfer via network. It's endianness-independent, making it possible to migrate the application's state between different platforms.

Many of the available serialization frameworks do not consider complex graph relations between objects. It's a common situation that serializing and deserializing two objects A and B referencing the same object C leaves you with two identical copies of C, one referenced by A and one referenced by B. Migrant takes such scenarios into account, preserving the identity of references, without further code decoration. Thanks to this, a programmer is relieved of implementing complex consistency mechanisms for the system and the resulting binary form is even smaller.

Migrant's ease of use does not prohibit the programmer from controlling the serialization behaviour in more complex scenarios. It is possible to hide some fields of a class, to deserialize objects using their custom constructors and to add hooks to the class code that will execute before or after (de)serialization. With little effort it is possible for the programmer to reimplement (de)serialization patterns for specific types.

Apart from the main serialization framework, we provide a mechanism to translate primitive .NET types (and some other) to their binary representation and push them to a stream. Such a form is very compact - Migrant uses the [Varint encoding](https://developers.google.com/protocol-buffers/docs/encoding#varints) and the [ZigZag encoding](https://developers.google.com/protocol-buffers/docs/encoding#varints). For example, serializing an Int64 variable with value 1 gives a smaller representation than Int32 with value 1000. Although CLS offers the BinaryWriter class, it is known to be quite clumsy and not very elegant to use.

Another extra feature, unavailable in convenient form in CLI, is an ability to deep clone given objects. With just one method invocation, Migrant will return an object copy, using the same mechanisms as the rest of the serialization framework.

Serialization and deserialization is done using on-line generated methods for
performance (a user can also use reflection instead if he wishes).

Migrant can also be configured to replace objects of given type with user provided objects during serialization or deserialization. The feature is known as **Surrogates**.

Performance benchmarks against other popular serialization frameworks are yet to be run, but initial testing is quite promising.

## Download

To download a precompiled version of Migrant, use [NuGet Package Manager](http://www.nuget.org/packages/Migrant/).

## Compilation

To compile the project, open the solution file in your IDE and hit compile. You may, alternatively, compile the library from the command line:

> msbuild Migrant.sln

or, under Mono:

> xbuild Migrant.sln

## Coding style

If you intend to help us developing Migrant, you're most welcome!

Please adhere to our coding style rules. For MonoDevelop, they are included in the .sln files. For Visual Studio we provide a .vssettings file you can import to your IDE.

In case of any doubts, just take a look to see how we have done it.

## More information

Additional information will soon be available on our [company's website](http://www.antmicro.com/OpenSource).

We are available on [github](https://www.github.com/antmicro) and [twitter](http://twitter.com/antmicro).

If you have any questions, suggestions or requests regarding the Migrant library, please do not hesitate to contact us via email: <migrant@antmicro.com>.

## Licence

Migrant is released on an MIT licence, which can be found in the LICENCE file in this directory.

## Authors

Migrant was created at [Antmicro Ltd](http://www.antmicro.com) by:

* Konrad Kruczyński (<kkruczynski@antmicro.com>)
* Mateusz Hołenko (<mholenko@antmicro.com>)
* Piotr Zierhoffer (<pzierhoffer@antmicro.com>)
* Michael Gielda (<mgielda@antmicro.com>)

Other contributors, in order of first contribution:

* Jeffrey Pierson (<jeffrey.pierson@gmail.com>)
* Roger Johansson (<rogeralsing@gmail.com>)
