using System;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class TypeDescriptor
    {
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

            var awaiter = _asyncTypeDescriptor.GetAwaiter(_instance);

            Assert(awaiter != null);
            Assert(_asyncTypeDescriptor.AwaiterType.IsAssignableFrom(awaiter.GetType()));

            return new AsyncTypeAwaiter(_asyncTypeDescriptor, awaiter);
        }
    }

    public readonly struct AsyncTypeAwaiter : INotifyCompletion
    {
        private readonly TypeDescriptor _asyncTypeDescriptor;
        private readonly object _awaiter;

        internal AsyncTypeAwaiter(TypeDescriptor asyncTypeDescriptor, object awaiter)
        {
            Assert(awaiter != null);
            Assert(asyncTypeDescriptor.AwaiterType.IsAssignableFrom(awaiter.GetType()));
            _asyncTypeDescriptor = asyncTypeDescriptor;
            _awaiter = awaiter;
        }

        public bool IsCompleted => _asyncTypeDescriptor != null ? _asyncTypeDescriptor.IsCompleted(_awaiter) : true;

        public void OnCompleted(Action continuation)
        {
            if (_asyncTypeDescriptor != null)
            {
                _asyncTypeDescriptor.OnCompleted(_awaiter, continuation);
            }
        }

        public object GetResult()
        {
            if (_asyncTypeDescriptor != null)
            {
                return _asyncTypeDescriptor.GetResult(_awaiter);
            }

            return null;
        }
    }
}
