Migrant 0.2.0
=============

This is the *Migrant* project, fast and flexible serialization framework usable for undecorated classes, written in C#.

Table of Contents
-----------------

#. Introduction
#. Directory organization
#. Features
#. Compilation
#. Usage
#. Referenced libraries
#. More information
#. Licence


Introduction
------------

Migrant is a serialization framework for .NET and Mono projects. It's aim is to provide an easy way to serialize complex graphs of objects, with minimal programming effort.

Directory organization
----------------------

There are three main directories:

Migrant
  contains the source code of the library,

Tests
  contains unit tests,

Lib
  contains required libraries.

There are two solution files - Migrant.sln, the core library, and MigrantWithTests.sln, combining both the test project and Migrant library.

Features
--------

Migrant is designed to be easy to use. For most cases, the scenario consists of calling one method to serialize, and another to deserialize a whole set of interconnected objects. It's not necessary to provide any information about serialized types, only the root object to save. All of the other objects referenced by the root are serialized automatically. It works out of the box for value and reference types, complex collections etc. While serialization of certain objects (e.g. pointers) is meaningless and may lead to hard-to-trace problems, Migrant will gracefully fail to serialize such objects, providing the programmer with full information on what caused the problem and where is it located.

The output of the serialization process is a stream of bytes, intended to reflect the memory organization of the actual system. This data format is compact and thus easy to transfer via network. It's endianness-independent, making it possible to migrate the application's state between different platforms.

Many of the available serialization frameworks do not consider complex graph relations between objects. It's a common situation that serializing and deserializing two objects A and B referencing the same object C leaves you with two identical copies of C, one referenced by A and one referenced by B. Migrant takes such scenarios into account, preserving the identity of references, without further code decoration. Thanks to this, a programmer is relieved of implementing complex consistency mechanisms for the system and the resulting binary form is even smaller.

Migrant's ease of use does not prohibit the programmer from controlling the serialization behaviour in more complex scenarios. It is possible to hide some fields of a class, to deserialize objects using their custom constructors and to add hooks to the class code that will execute before or after (de)serialization. With little effort it is possible for the programmer to reimplement (de)serialization patterns for specific types.

Apart from the main serialization framework, we provide a mechanism to translate primitive .NET types (and some other) to their binary representation and push them to a stream. Such a form is very compact - the number of bytes used by a serialized object is not based on its type, but on the object's value, using the `Varint encoding <https://developers.google.com/protocol-buffers/docs/encoding#varints>`_ and, optionally, `ZigZag encoding <https://developers.google.com/protocol-buffers/docs/encoding#varints>`_. For example, serializing an Int64 variable with value 1 gives a smaller representation than Int32 with value 1000. Although CLS offers the BinaryWriter class, it is known to be quite clumsy and not very elegant to use. 

Another extra feature, unavailable in convenient form in CLI, is an ability to deep clone given objects. With just one method invocation, Migrant will return an object copy, using the same mechanisms as the rest of the serialization framework.

The current release of Migrant uses reflection mechanisms to handle the deserialization, however the serialization is done using on-line generated methods for performance (the user can also use the reflection instead if he wishes). In the next release we plan to offer deserialization using generated methods as well.

Migrant can also be configured to replace objects of given type with user provided objects during serialization or deserialization. The feature is known as **Surrogates**.

Performance benchmarks against other popular serialization frameworks are yet to be run, but initial testing is quite promising.

Compilation
-----------

To compile the project, open the solution file in your IDE and hit compile. You may, alternatively, compile the library from the command line:

  msbuild Migrant.sln

or, under Mono:

  xbuild Migrant.sln

Usage
-----

Here we present some simple use cases of Migrant. They are written in pseudo-C#, but can be easily translated to other CLI languages.

Simple serialization
++++++++++++++++++++

::
  
  var stream = new MyCustomStream();
  var myComplexObject = new MyComplexType(complexParameters);
  var serializer = new Serializer();

  //_Optional_ step to prepare the framework, so the actual serialization is even faster
  serializer.Initialize(typeof(MyComplexType));

  serializer.Serialize(myComplexObject, stream);

  stream.Rewind();

  var myDeserializedObject = serializer.Deserialize<MyComplexType>(stream);

Deep clone
++++++++++

::
  
  var myComplexObject = new MyComplexType(complexParameters);
  var myObjectCopy = Serializer.DeepCopy(myComplexObject);


Simple types to bytes
+++++++++++++++++++++

::
  
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

Surrogates
++++++++++

::

  var serializer = new Serializer();
  var someObject = new SomeObject();
  serializer.ForObject<SomeObject>().SetSurrogate(x => new AnotherObject());
  serializer.Serialize(someObject, stream);

  stream.Rewind();

  var anObject = serializer.Deserialize<object>(stream);
  Console.WriteLine(anObject.GetType().Name); // prints AnotherObject

Referenced libraries
--------------------

   ImpromptuInterface >= 5.6.7

More information
----------------

Additional information will soon be available on our `company's website <http://www.antmicro.com/OpenSource>`_.

We are available on github_ and twitter_.

If you have any questions, suggestions or requests regarding the Migrant library, please do not hesitate to contact us via email: `migrant@antmicro.com <mailto:migrant@antmicro.com>`_.

.. _github: https://www.github.com/antmicro

.. _twitter: http://twitter.com/antmicro

Licence
-------

Migrant is released on an MIT licence, which can be found in LICENCE file in this directory.
