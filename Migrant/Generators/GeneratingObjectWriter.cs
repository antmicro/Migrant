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
using AntMicro.Migrant;
using System.IO;
using System.Collections.Generic;
using System;
using System.Reflection.Emit;
using System.Reflection;
using AntMicro.Migrant.Generators;

namespace AntMicro.Migrant.Emitter
{
	internal class GeneratingObjectWriter : ObjectWriter
	{
		public GeneratingObjectWriter(Stream stream, IList<Type> upfrontKnownTypes, Action<object> preSerializationCallback = null, 
		                              Action<object> postSerializationCallback = null, IDictionary<Type, MethodInfo> writeMethodCache = null)
			: base(stream, upfrontKnownTypes, preSerializationCallback, postSerializationCallback)
		{
			transientTypes = new Dictionary<Type, bool>();
			writeMethods = new Action<PrimitiveWriter, object>[0];
			this.writeMethodCache = writeMethodCache;
			RegenerateWriteMethods();
		}

		internal void WriteObjectId(object o)
		{
			// this function is called when object to serialize cannot be data-inlined object such as string
			Writer.Write(Identifier.GetId(o));
		}

		internal void WriteObjectIdPossiblyInline(object o)
		{
			var refId = Identifier.GetId(o);
			var type = o.GetType();
			Writer.Write(refId);
			if(ShouldBeInlined(type, refId))
			{
				InlineWritten.Add(refId);
                InvokeCallbacksAndWriteObject(o);
			}
		}

		internal bool CheckTransient(object o)
		{
			return CheckTransient(o.GetType());
		}

		internal bool CheckTransient(Type type)
		{
			bool result;
			if(transientTypes.TryGetValue(type, out result))
			{
				return result;
			}
			var isTransient = type.IsDefined(typeof(TransientAttribute), false);
			transientTypes.Add(type, isTransient);
			return isTransient;
		}

		protected internal override void WriteObjectInner(object o)
		{
			var type = o.GetType();
            var typeId = TouchAndWriteTypeId(type);
			writeMethods[typeId](Writer, o);
		}

		protected override void AddMissingType(Type type)
		{
			base.AddMissingType(type);
			RegenerateWriteMethods();
		}

		private void RegenerateWriteMethods()
		{
			var newWriteMethods = new Action<PrimitiveWriter, object>[TypeIndices.Count];
			foreach(var entry in TypeIndices)
			{
				if(writeMethods.Length > entry.Value)
				{
					newWriteMethods[entry.Value] = writeMethods[entry.Value];
				}
				else
				{
					if(!CheckTransient(entry.Key))
					{
						if(writeMethodCache != null && writeMethodCache.ContainsKey(entry.Key))
						{
							newWriteMethods[entry.Value] = (Action<PrimitiveWriter, object>)
								Delegate.CreateDelegate(typeof(Action<PrimitiveWriter, object>), this, writeMethodCache[entry.Key]);
						}
						else
						{
							newWriteMethods[entry.Value] = GenerateWriteMethod(entry.Key);
						}
					}
					// for transient class the delegate will never be called
				}
			}
			writeMethods = newWriteMethods;
		}

		private Action<PrimitiveWriter, object> GenerateWriteMethod(Type actualType)
		{
			var specialWrite = LinkSpecialWrite(actualType);
			if(specialWrite != null)
			{
				// linked methods are not added to writeMethodCache, there's no point
				return specialWrite;
			}

			var method = new WriteMethodGenerator(actualType, TypeIndices).Method;
			var result = (Action<PrimitiveWriter, object>)method.CreateDelegate(typeof(Action<PrimitiveWriter, object>), this);
			if(writeMethodCache != null)
			{
				writeMethodCache.Add(actualType, result.Method);
			}
			return result;
		}

		private Action<PrimitiveWriter, object> LinkSpecialWrite(Type actualType)
		{
			if(actualType == typeof(string))
			{
				return (y, obj) => y.Write((string)obj);
			}
			if(typeof(ISpeciallySerializable).IsAssignableFrom(actualType))
			{
				return (writer, obj) => {
					Console.WriteLine (this);
					var startingPosition = writer.Position;
	                ((ISpeciallySerializable)obj).Save(writer);
	                writer.Write(writer.Position - startingPosition);
				};
			}
			return null;
		}

		// TODO: actually, this field can be considered static
		private readonly Dictionary<Type, bool> transientTypes;
		private readonly IDictionary<Type, MethodInfo> writeMethodCache;
		private Action<PrimitiveWriter, object>[] writeMethods;
	}
}

