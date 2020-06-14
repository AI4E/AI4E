/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AI4E.Utils.Async
{
    /// <summary>
    /// Represents a descriptor for awaitable types.
    /// </summary>
    public sealed class AwaitableTypeDescriptor
    {
        private static readonly Type[] _singleActionParameter = new[] { typeof(Action) };
        private static readonly ParameterModifier[] _emptyParameterModifiers = Array.Empty<ParameterModifier>();

        // This is a conditional weak table to allow assembly unloading.
        private static readonly ConditionalWeakTable<Type, AwaitableTypeDescriptor> _cache
            = new ConditionalWeakTable<Type, AwaitableTypeDescriptor>();

        private static readonly MethodInfo _notifyCompletionOnCompletedMethod
            = typeof(INotifyCompletion).GetMethod(
                nameof(INotifyCompletion.OnCompleted),
                BindingFlags.Instance | BindingFlags.Public,
                Type.DefaultBinder,
                _singleActionParameter,
                _emptyParameterModifiers)!;

        /// <summary>
        /// Returns the descriptor for the specified type.
        /// </summary>
        /// <param name="type">A <see cref="System.Type"/> to get the descriptor for.</param>
        /// <returns>The <see cref="AwaitableTypeDescriptor"/> taht describes <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is <c>null</c>.</exception>
        public static AwaitableTypeDescriptor GetTypeDescriptor(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var result = _cache.GetValue(type, BuildTypeDescriptor);
            return result;
        }

        private static AwaitableTypeDescriptor BuildTypeDescriptor(Type type)
        {
            // We check, whether we can await the type with the same pattern, the compiler uses.
            // TODO: Is there a way, we can include extension methods?
            var awaiterMethod = type.GetMethod(nameof(Task.GetAwaiter),
                                               BindingFlags.Instance | BindingFlags.Public,
                                               Type.DefaultBinder,
                                               Type.EmptyTypes,
                                               _emptyParameterModifiers);

            if (awaiterMethod == null)
            {
                return new AwaitableTypeDescriptor(type);
            }

            var awaiterType = awaiterMethod.ReturnType;

            if (awaiterType == typeof(void))
            {
                return new AwaitableTypeDescriptor(type);
            }

            if (!awaiterType.GetInterfaces().Any(p => p == typeof(INotifyCompletion)))
            {
                return new AwaitableTypeDescriptor(type);
            }

            var isCompletedProperty = awaiterType.GetProperty(nameof(TaskAwaiter.IsCompleted),
                                                              BindingFlags.Instance | BindingFlags.Public,
                                                              Type.DefaultBinder,
                                                              typeof(bool),
                                                              Type.EmptyTypes,
                                                              _emptyParameterModifiers);

            if (isCompletedProperty == null)
            {
                return new AwaitableTypeDescriptor(type);
            }

            var getResultMethod = awaiterType.GetMethod(nameof(TaskAwaiter.GetResult),
                                                        BindingFlags.Instance | BindingFlags.Public,
                                                        Type.DefaultBinder,
                                                        Type.EmptyTypes,
                                                        _emptyParameterModifiers);

            if (getResultMethod == null)
            {
                return new AwaitableTypeDescriptor(type);
            }

            var resultType = getResultMethod.ReturnType;

            var instance = Expression.Parameter(typeof(object), "instance");
            var convertedInstance = Expression.Convert(instance, type);
            var getAwaiterCall = Expression.Call(convertedInstance, awaiterMethod);
            var compiledGetAwaiterCall = Expression.Lambda<Func<object, object>>(
                Expression.Convert(getAwaiterCall, typeof(object)), instance).Compile();

            var awaiter = Expression.Parameter(typeof(object), "awaiter");
            var convertedAwaiter = Expression.Convert(awaiter, awaiterType);
            var isCompletedPropertyAccess = Expression.Property(convertedAwaiter, isCompletedProperty);
            var compiledIsCompletedPropertyAccess = Expression.Lambda<Func<object, bool>>(
                isCompletedPropertyAccess, awaiter).Compile();

            var getResultCall = Expression.Call(convertedAwaiter, getResultMethod);
            Func<object, object?> compiledGetResultCall;

            if (resultType == typeof(void))
            {
                var voidGetResultCall = Expression.Lambda<Action<object>>(getResultCall, awaiter).Compile();
                compiledGetResultCall = o => { voidGetResultCall(o); return null; };
            }
            else
            {
                compiledGetResultCall = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(getResultCall, typeof(object)), awaiter).Compile();
            }

            // We search for an implicit interface implementation
            // to prevent boxing for the case the type is a value type.
            var onCompletedMethod = awaiterType.GetMethod(nameof(INotifyCompletion.OnCompleted),
                                                          BindingFlags.Instance | BindingFlags.Public,
                                                          Type.DefaultBinder,
                                                          _singleActionParameter,
                                                          _emptyParameterModifiers);

            Action<object, Action> compiledOnCompletedCall;
            var continuationParameter = Expression.Parameter(typeof(Action), "continuation");

            if (onCompletedMethod == null)
            {
                onCompletedMethod = _notifyCompletionOnCompletedMethod;

                Debug.Assert(onCompletedMethod != null);

                var convertedToInterfaceAwaiter = Expression.Convert(awaiter, typeof(INotifyCompletion));
                var onCompletedMethodCall = Expression.Call(
                    convertedToInterfaceAwaiter, onCompletedMethod, continuationParameter);
                compiledOnCompletedCall = Expression.Lambda<Action<object, Action>>(
                    onCompletedMethodCall, awaiter, continuationParameter).Compile();
            }
            else
            {
                var onCompletedMethodCall = Expression.Call(convertedAwaiter, onCompletedMethod, continuationParameter);
                compiledOnCompletedCall = Expression.Lambda<Action<object, Action>>(
                    onCompletedMethodCall, awaiter, continuationParameter).Compile();
            }

            return new AwaitableTypeDescriptor(
                type,
                resultType,
                awaiterType,
                compiledGetAwaiterCall,
                compiledIsCompletedPropertyAccess,
                compiledOnCompletedCall,
                compiledGetResultCall);
        }

        internal AwaitableTypeDescriptor(
            Type type,
            Type resultType,
            Type awaiterType,
            Func<object, object> getAwaiter,
            Func<object, bool> isCompleted,
            Action<object, Action> onCompleted,
            Func<object, object?> getResult)
        {
            Type = type;
            ResultType = resultType;
            AwaiterType = awaiterType;
            IsAwaitable = true;

            GetAwaiter = getAwaiter;
            IsCompleted = isCompleted;
            OnCompleted = onCompleted;
            GetResult = getResult;
        }

        internal AwaitableTypeDescriptor(Type type)
        {
            Type = type;
            ResultType = type;
            AwaiterType = null;
            IsAwaitable = false;

            GetAwaiter = null;
            IsCompleted = null;
            OnCompleted = null;
            GetResult = null;
        }

        /// <summary>
        /// Gets a boolean value indicating whether <see cref="Type"/> is awaitable.
        /// </summary>
        public bool IsAwaitable { get; }

        /// <summary>
        /// Gets the type that is describes by the current instance.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the type that is the result of awaiting <see cref="Type"/>
        /// or <see cref="Type"/> if it is not awaitable.
        /// </summary>
        public Type ResultType { get; }

        /// <summary>
        /// Gets the <see cref="System.Type"/> of awaiters of <see cref="Type"/>
        /// or <c>null</c> of it is not awaitable.
        /// </summary>
        public Type? AwaiterType { get; }
        internal Func<object, object>? GetAwaiter { get; }
        internal Func<object, bool>? IsCompleted { get; }
        internal Action<object, Action>? OnCompleted { get; }
        internal Func<object, object?>? GetResult { get; }

        /// <summary>
        /// Gets an <see cref="AsyncTypeAwaitable"/> that can be used to await the specified instance.
        /// </summary>
        /// <param name="instance">An <see cref="object"/> that needs to be awaited.</param>
        /// <returns>A <see cref="AsyncTypeAwaitable"/> that can be used to await <paramref name="instance"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown of <paramref name="instance"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <see cref="Type"/> is not assignable from the type of <paramref name="instance"/>.
        /// </exception>
        /// <remarks>
        /// If <see cref="Type"/> is not awaitable, this creates a wrapped around <paramref name="instance"/>
        /// that synachronously returns <paramref name="instance"/> when awaited.
        /// </remarks>
        public AsyncTypeAwaitable GetAwaitable(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (!Type.IsAssignableFrom(instance.GetType()))
                throw new ArgumentException(
                    $"The argument must be of type '{Type.ToString()}' or an assignable type.", nameof(instance));

            return new AsyncTypeAwaitable(this, instance);
        }
    }

    /// <summary>
    /// An awaitable type that is used to wrap objects that need to be awaited.
    /// </summary>
    /// <remarks>
    /// This is not meant to be used directly but to be awaited via the compilers <c>await</c> keyword.
    /// </remarks>
#pragma warning disable CA1815
    public readonly struct AsyncTypeAwaitable
#pragma warning restore CA1815
    {
        private readonly AwaitableTypeDescriptor _awaitableTypeDescriptor;
        private readonly object _instance;

        internal AsyncTypeAwaitable(AwaitableTypeDescriptor awaitableTypeDescriptor, object instance)
        {
            Debug.Assert(awaitableTypeDescriptor.Type.IsAssignableFrom(instance.GetType()));
            _awaitableTypeDescriptor = awaitableTypeDescriptor;
            _instance = instance;
        }

        /// <summary>
        /// Gets an awaiter for the current instance.
        /// </summary>
        /// <returns>The awaiter for the current instance.</returns>
        public AsyncTypeAwaiter GetAwaiter()
        {
            if (_awaitableTypeDescriptor == null)
                return default;

            return new AsyncTypeAwaiter(_awaitableTypeDescriptor, _instance);
        }
    }


    /// <summary>
    /// An awaiter for objects that need to be awaited.
    /// </summary>
#pragma warning disable CA1815
    public readonly struct AsyncTypeAwaiter : INotifyCompletion
#pragma warning restore CA1815
    {
        private readonly AwaitableTypeDescriptor _awaitableTypeDescriptor;

        // Awaiter if _asnycTypeDescriptorIsAsyncType is true, Instance otherwise.
        private readonly object _instanceOrAwaiter;

        internal AsyncTypeAwaiter(AwaitableTypeDescriptor awaitableTypeDescriptor, object instance)
        {
            _awaitableTypeDescriptor = awaitableTypeDescriptor;

            if (awaitableTypeDescriptor.IsAwaitable)
            {
                _instanceOrAwaiter = awaitableTypeDescriptor.GetAwaiter!.Invoke(instance);

                Debug.Assert(_instanceOrAwaiter != null);
                Debug.Assert(awaitableTypeDescriptor.AwaiterType!.IsAssignableFrom(_instanceOrAwaiter!.GetType()));
            }
            else
            {
                _instanceOrAwaiter = instance;
            }
        }

        /// <inheritdoc />
        public bool IsCompleted
        {
            get
            {
                if (_awaitableTypeDescriptor == null)
                    return true;

                if (!_awaitableTypeDescriptor.IsAwaitable)
                    return true;

                return _awaitableTypeDescriptor.IsCompleted!.Invoke(_instanceOrAwaiter);
            }
        }

        /// <inheritdoc />
        public void OnCompleted(Action continuation)
        {
            if (_awaitableTypeDescriptor != null)
            {
                if (_awaitableTypeDescriptor.IsAwaitable)
                {
                    _awaitableTypeDescriptor.OnCompleted!.Invoke(_instanceOrAwaiter, continuation);
                }
                else
                {
                    continuation?.Invoke();
                }
            }
        }

        /// <inheritdoc />
        public object? GetResult()
        {
            if (_awaitableTypeDescriptor != null)
            {
                if (_awaitableTypeDescriptor.IsAwaitable)
                {
                    return _awaitableTypeDescriptor.GetResult!.Invoke(_instanceOrAwaiter);
                }
                else
                {
                    return _instanceOrAwaiter;
                }
            }

            return null;
        }
    }
}
