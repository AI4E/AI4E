using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class TypeDescriptor
    {
        private static readonly Type[] _singleActionParameter = new[] { typeof(Action) };
        private static readonly ParameterModifier[] _emptyParameterModifiers = new ParameterModifier[0];
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache;
        private static readonly MethodInfo _notifyCompletionOnCompletedMethod;

        static TypeDescriptor()
        {
            _notifyCompletionOnCompletedMethod = typeof(INotifyCompletion).GetMethod(nameof(INotifyCompletion.OnCompleted),
                                                                                     BindingFlags.Instance | BindingFlags.Public,
                                                                                     Type.DefaultBinder,
                                                                                     _singleActionParameter,
                                                                                     _emptyParameterModifiers);

            _cache = new ConcurrentDictionary<Type, TypeDescriptor>();
        }

        public static TypeDescriptor GetTypeDescriptor(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var result = _cache.GetOrAdd(type, BuildTypeDescriptor);
            Assert(result != null);
            return result;
        }

        private static TypeDescriptor BuildTypeDescriptor(Type type)
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
                return new TypeDescriptor(type);
            }

            var awaiterType = awaiterMethod.ReturnType;

            if (awaiterType == typeof(void))
            {
                return new TypeDescriptor(type);
            }

            if (!awaiterType.GetInterfaces().Any(p => p == typeof(INotifyCompletion)))
            {
                return new TypeDescriptor(type);
            }

            var isCompletedProperty = awaiterType.GetProperty(nameof(TaskAwaiter.IsCompleted),
                                                              BindingFlags.Instance | BindingFlags.Public,
                                                              Type.DefaultBinder,
                                                              typeof(bool),
                                                              Type.EmptyTypes,
                                                              _emptyParameterModifiers);

            if (isCompletedProperty == null)
            {
                return new TypeDescriptor(type);
            }

            var getResultMethod = awaiterType.GetMethod(nameof(TaskAwaiter.GetResult),
                                                        BindingFlags.Instance | BindingFlags.Public,
                                                        Type.DefaultBinder,
                                                        Type.EmptyTypes,
                                                        _emptyParameterModifiers);

            if (getResultMethod == null)
            {
                return new TypeDescriptor(type);
            }

            var resultType = getResultMethod.ReturnType;

            var instance = Expression.Parameter(typeof(object), "instance");
            var convertedInstance = Expression.Convert(instance, type);
            var getAwaiterCall = Expression.Call(convertedInstance, awaiterMethod);
            var compiledGetAwaiterCall = Expression.Lambda<Func<object, object>>(Expression.Convert(getAwaiterCall, typeof(object)), instance).Compile();

            var awaiter = Expression.Parameter(typeof(object), "awaiter");
            var convertedAwaiter = Expression.Convert(awaiter, awaiterType);
            var isCompletedPropertyAccess = Expression.Property(convertedAwaiter, isCompletedProperty);
            var compiledIsCompletedPropertyAccess = Expression.Lambda<Func<object, bool>>(isCompletedPropertyAccess, awaiter).Compile();

            var getResultCall = Expression.Call(convertedAwaiter, getResultMethod);
            Func<object, object> compiledGetResultCall;

            if (resultType == typeof(void))
            {
                var voidGetResultCall = Expression.Lambda<Action<object>>(getResultCall, awaiter).Compile();
                compiledGetResultCall = o => { voidGetResultCall(o); return null; };
            }
            else
            {
                compiledGetResultCall = Expression.Lambda<Func<object, object>>(Expression.Convert(getResultCall, typeof(object)), awaiter).Compile();
            }

            // We search for an implicit interface implementation to prevent boxing for the case the type is a value type.
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

                Assert(onCompletedMethod != null);

                var convertedToInterfaceAwaiter = Expression.Convert(awaiter, typeof(INotifyCompletion));
                var onCompletedMethodCall = Expression.Call(convertedToInterfaceAwaiter, onCompletedMethod, continuationParameter);
                compiledOnCompletedCall = Expression.Lambda<Action<object, Action>>(onCompletedMethodCall, awaiter, continuationParameter).Compile();
            }
            else
            {
                var onCompletedMethodCall = Expression.Call(convertedAwaiter, onCompletedMethod, continuationParameter);
                compiledOnCompletedCall = Expression.Lambda<Action<object, Action>>(onCompletedMethodCall, awaiter, continuationParameter).Compile();
            }

            return new TypeDescriptor(type, resultType, awaiterType, compiledGetAwaiterCall, compiledIsCompletedPropertyAccess, compiledOnCompletedCall, compiledGetResultCall);
        }

        internal TypeDescriptor(Type type,
                                Type resultType,
                                Type awaiterType,
                                Func<object, object> getAwaiter,
                                Func<object, bool> isCompleted,
                                Action<object, Action> onCompleted,
                                Func<object, object> getResult)
        {
            Assert(type != null);
            Assert(resultType != null);
            Assert(awaiterType != null);
            Assert(getAwaiter != null);
            Assert(isCompleted != null);
            Assert(onCompleted != null);
            Assert(getResult != null);

            Type = type;
            ResultType = resultType;
            AwaiterType = awaiterType;
            IsAsyncType = true;

            GetAwaiter = getAwaiter;
            IsCompleted = isCompleted;
            OnCompleted = onCompleted;
            GetResult = getResult;
        }

        internal TypeDescriptor(Type type)
        {
            Type = type;
            ResultType = type;
            AwaiterType = null;
            IsAsyncType = false;

            GetAwaiter = null;
            IsCompleted = null;
            OnCompleted = null;
            GetResult = null;
        }

        public bool IsAsyncType { get; }
        public Type Type { get; }
        public Type ResultType { get; }
        public Type AwaiterType { get; }
        internal Func<object, object> GetAwaiter { get; }
        internal Func<object, bool> IsCompleted { get; }
        internal Action<object, Action> OnCompleted { get; }
        internal Func<object, object> GetResult { get; }

        public AsyncTypeAwaitable GetAwaitable(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (!Type.IsAssignableFrom(instance.GetType()))
                throw new ArgumentException($"The argument must be of type '{Type.ToString()}' or an assignable type.", nameof(instance));

            if (!IsAsyncType)
                throw new InvalidOperationException();

            return new AsyncTypeAwaitable(this, instance);
        }
    }

    public readonly struct AsyncTypeAwaitable
    {
        private readonly TypeDescriptor _asyncTypeDescriptor;
        private readonly object _instance;

        public AsyncTypeAwaitable(TypeDescriptor asyncTypeDescriptor, object instance)
        {
            Assert(instance != null);
            Assert(asyncTypeDescriptor.Type.IsAssignableFrom(instance.GetType()));
            _asyncTypeDescriptor = asyncTypeDescriptor;
            _instance = instance;
        }

        public AsyncTypeAwaiter GetAwaiter()
        {
            if (_asyncTypeDescriptor == null)
                return default;

            return new AsyncTypeAwaiter(_asyncTypeDescriptor, _instance);
        }
    }

    public readonly struct AsyncTypeAwaiter : INotifyCompletion
    {
        private readonly TypeDescriptor _asyncTypeDescriptor;

        // Awaiter if _asnycTypeDescriptorIsAsyncType is true, Instance otherwise.
        private readonly object _instanceOrAwaiter;

        internal AsyncTypeAwaiter(TypeDescriptor asyncTypeDescriptor, object instance)
        {
            Assert(instance != null);

            _asyncTypeDescriptor = asyncTypeDescriptor;

            if (asyncTypeDescriptor.IsAsyncType)
            {
                _instanceOrAwaiter = asyncTypeDescriptor.IsAsyncType ?
                                      asyncTypeDescriptor.GetAwaiter(instance) : null;

                Assert(_instanceOrAwaiter != null);
                Assert(asyncTypeDescriptor.AwaiterType.IsAssignableFrom(_instanceOrAwaiter.GetType()));
            }
            else
            {
                _instanceOrAwaiter = instance;
            }
        }

        public bool IsCompleted
        {
            get
            {
                if (_asyncTypeDescriptor == null)
                    return true;

                if (!_asyncTypeDescriptor.IsAsyncType)
                    return true;

                return _asyncTypeDescriptor.IsCompleted(_instanceOrAwaiter);
            }
        }

        public void OnCompleted(Action continuation)
        {
            if (_asyncTypeDescriptor != null)
            {
                if (_asyncTypeDescriptor.IsAsyncType)
                {
                    _asyncTypeDescriptor.OnCompleted(_instanceOrAwaiter, continuation);
                }
                else
                {
                    continuation();
                }
            }
        }

        public object GetResult()
        {
            if (_asyncTypeDescriptor != null)
            {
                if (_asyncTypeDescriptor.IsAsyncType)
                {
                    return _asyncTypeDescriptor.GetResult(_instanceOrAwaiter);
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
