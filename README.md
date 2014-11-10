# Migrant 0.7

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

## Introduction

Migrant is a serialization framework for .NET and Mono projects. Its aim is to provide an easy way to serialize complex graphs of objects, with minimal programming effort.

## Directory organization

There are three main directories:

- ``Migrant`` - contains the source code of the library;
- ``Tests`` - contains unit tests (we're using NUnit);
- ``PerformanceTests`` - contains performance assessment project, based on NUnit;
- ``ResultBrowser`` - simple command line tool to browse performance test results history;
- ``Lib`` - contains required third party libraries (for all the projects).

There are two solution files - Migrant.sln, the core library, and MigrantWithTests.sln, combining both the test project and Migrant library.

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

As default, all writes to the stream are buffered, i.e. one can be only sure that they are in the stream after calling `Dispose()` on open stream (de)serializer. This is, however, not useful when underlying stream is buffered independently or it is a network stream attached to a socket. In such cases one can disable buffering in `Settings`.

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

### Version tolerance

What if some changes are made to the layout of the class between serialization and deserialization? Migrant can cope with that up to some extent. During creation of serializer you can specify settings, among which there is a version tolerance level. This is an enumeration with five possible values:

- ``GUID`` - the most restrictive option. Deserialization is possible if module ID (which is GUID generated when module is compiled) is the same as it was during serialization. In other words, serialization and deserialization must be done with the same assembly.
- ``Exact`` - this is a default value. Deserialization is possible if no fields are added or removed and no type changes were done.
- ``FieldAddition`` - new version of the type can contain more fields than it contained during serialization. They are initialized with their default values.
- ``FieldRemoval`` - new version of the type can contain less fields than it contained during serialization.
- ``FieldAdditionAndRemoval`` - combination of these two above.

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

## More information

Additional information will soon be available on our [company's website](http://www.antmicro.com/OpenSource).

We are available on [github](https://www.github.com/antmicro) and [twitter](http://twitter.com/antmicro).

If you have any questions, suggestions or requests regarding the Migrant library, please do not hesitate to contact us via email: [[migrant@antmicro.com](mailto:migrant@antmicro.com)](mailto:migrant@antmicro.com).

## Licence

Migrant is released on an MIT licence, which can be found in LICENCE file in this directory.
