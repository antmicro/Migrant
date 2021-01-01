// *******************************************************************
//
//  Copyright (c) 2012-2016, Antmicro Ltd <antmicro.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Migrantoid.Hooks;
using Migrantoid.Utilities;

namespace Migrantoid.Generators
{
    internal class CompletedGenerator : DynamicReadMethodGenerator<CompleteMethodDelegate>
    {
        public CompletedGenerator(Type type, SwapList objectsForSurrogates, bool disableStamping, bool treatCollectionAsUserObject, bool callPostDeserializationCallback)
            : base(type, "ObjectCompleted", disableStamping, treatCollectionAsUserObject, OpCodes.Ldarg_1, OpCodes.Ldarg_0)
        {
            this.objectsForSurrogates = objectsForSurrogates;
            this.callPostDeserializationCallback = callPostDeserializationCallback;
        }

        protected override void InnerGenerate(ReaderGenerationContext context)
        {
            var factoryId = objectsForSurrogates.FindMatchingIndex(type);

            GenerateCallPostDeserializationHooks(context, factoryId);
            GenerateDesurrogate(context, factoryId);
        }

        private void GenerateCallPostDeserializationHooks(ReaderGenerationContext context, int factoryId)
        {
            var methods = Helpers.GetMethodsWithAttribute(typeof(PostDeserializationAttribute), type).ToArray();
            foreach(var method in methods)
            {
                if(!method.IsStatic)
                {
                    context.PushDeserializedObjectOntoStack(context.PushObjectIdOntoStack);
                    context.Generator.Emit(OpCodes.Castclass, method.ReflectedType);
                }
                if(method.IsVirtual)
                {
                    context.Generator.Emit(OpCodes.Callvirt, method);
                }
                else
                {
                    context.Generator.Emit(OpCodes.Call, method);
                }
            }

            methods = Helpers.GetMethodsWithAttribute(typeof(LatePostDeserializationAttribute), type).ToArray();
            if(factoryId != -1 && methods.Length != 0)
            {
                throw new InvalidOperationException(
                    string.Format(ObjectReader.LateHookAndSurrogateError, type));
            }

            foreach(var method in methods)
            {
                context.PushObjectReaderOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectReader, List<Action>>(x => x.latePostDeserializationHooks);

                context.Generator.PushTypeOntoStack(typeof(Action));
                if(method.IsStatic)
                {
                    context.Generator.Emit(OpCodes.Ldnull);
                }
                else
                {
                    context.PushDeserializedObjectOntoStack(context.PushObjectIdOntoStack);
                    context.Generator.Emit(OpCodes.Castclass, method.ReflectedType);
                }

                context.Generator.Emit(OpCodes.Ldtoken, method);
                if(method.DeclaringType.IsGenericType)
                {
                    context.Generator.Emit(OpCodes.Ldtoken, method.DeclaringType);

                    context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, MethodBase>(x => MethodBase.GetMethodFromHandle(new RuntimeMethodHandle(), new RuntimeTypeHandle())));
                }
                else
                {
                    context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, MethodBase>(x => MethodBase.GetMethodFromHandle(new RuntimeMethodHandle())));
                }

                context.Generator.Emit(OpCodes.Castclass, typeof(MethodInfo));
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<object, Delegate>(x => Delegate.CreateDelegate(null, null, method)));

                context.Generator.Emit(OpCodes.Castclass, typeof(Action));

                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<List<Action>>(x => x.Add(null)));
            }

            if(callPostDeserializationCallback)
            {
                context.PushObjectReaderOntoStack();
                context.Generator.PushFieldValueOntoStack<ObjectReader, Action<object>>(x => x.postDeserializationCallback);
                context.PushDeserializedObjectOntoStack(context.PushObjectIdOntoStack);
                context.Generator.Emit(OpCodes.Call, Helpers.GetMethodInfo<Action<object>>(x => x.Invoke(null)));
            }
        }

        private void GenerateDesurrogate(ReaderGenerationContext context, int factoryId)
        {
            if(factoryId == -1)
            {
                return;
            }

            var desurrogatedObjectLocal = context.Generator.DeclareLocal(typeof(object));

            // obtain surrogate factory
            context.PushObjectReaderOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectReader, SwapList>(x => x.objectsForSurrogates);
            context.Generator.PushIntegerOntoStack(factoryId);
            context.Generator.Call<SwapList>(x => x.GetByIndex(0));

            // recreate an object from the surrogate
            var delegateType = typeof(Func<,>).MakeGenericType(type, typeof(object));
            context.Generator.Emit(OpCodes.Castclass, delegateType);
            context.PushDeserializedObjectOntoStack(context.PushObjectIdOntoStack);
            context.Generator.Emit(OpCodes.Call, delegateType.GetMethod("Invoke"));

            context.Generator.StoreLocalValueFromStack(desurrogatedObjectLocal);

            // clone context
            context.PushObjectReaderOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectReader, Serializer.ReadMethods>(x => x.readMethods);
            context.Generator.PushFieldValueOntoStack<Serializer.ReadMethods, DynamicMethodProvider<CloneMethodDelegate>>(x => x.cloneContentMehtodsProvider);
            context.Generator.PushLocalValueOntoStack(desurrogatedObjectLocal);
            context.Generator.Call<object>(x => x.GetType());
            context.Generator.Call<DynamicMethodProvider<CloneMethodDelegate>>(x => x.GetOrCreate(typeof(void)));
            context.Generator.PushLocalValueOntoStack(desurrogatedObjectLocal);
            context.PushDeserializedObjectOntoStack(context.PushObjectIdOntoStack, true);
            context.Generator.Call<CloneMethodDelegate>(x => x.Invoke(null, null));

            //remove object reference from surrogatesWhileReading collection
            context.PushObjectReaderOntoStack();
            context.Generator.PushFieldValueOntoStack<ObjectReader, OneToOneMap<int, object>>(x => x.surrogatesWhileReading);

            context.PushObjectIdOntoStack();
            context.Generator.Call<OneToOneMap<int, object>>(x => x.Remove(0));
        }

        private readonly SwapList objectsForSurrogates;
        private readonly bool callPostDeserializationCallback;
    }
}

