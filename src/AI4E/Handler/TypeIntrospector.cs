using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AI4E.Handler
{
    public static class TypeIntrospector
    {
        private static readonly ParameterModifier[] _emptyParameterModifiers = new ParameterModifier[0];
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache
            = new ConcurrentDictionary<Type, TypeDescriptor>();

        public static TypeDescriptor GetTypeDescriptor(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _cache.GetOrAdd(type, BuildTypeDescriptor);
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
                return default;
            }

            var awaiterType = awaiterMethod.ReturnType;

            if (awaiterType == typeof(void))
            {
                return default;
            }

            if (!awaiterType.GetInterfaces().Any(p => p == typeof(INotifyCompletion)))
            {
                return default;
            }

            var isCompletedProperty = awaiterType.GetProperty(nameof(TaskAwaiter.IsCompleted),
                                                              BindingFlags.Instance | BindingFlags.Public,
                                                              Type.DefaultBinder,
                                                              typeof(bool),
                                                              Type.EmptyTypes,
                                                              _emptyParameterModifiers);

            if (isCompletedProperty == null)
            {
                return default;
            }

            var getResultMethod = awaiterType.GetMethod(nameof(TaskAwaiter.GetResult),
                                                        BindingFlags.Instance | BindingFlags.Public,
                                                        Type.DefaultBinder,
                                                        Type.EmptyTypes,
                                                        _emptyParameterModifiers);

            if (getResultMethod == null)
            {
                return default;
            }

            var resultType = getResultMethod.ReturnType;

            var instance = Expression.Parameter(typeof(object), "instance");
            var convertedInstance = Expression.Convert(instance, type);
            var getAwaiterCall = Expression.Call(convertedInstance, awaiterMethod);
            var compiledGetAwaiterCall = Expression.Lambda<Func<object, object>>(getAwaiterCall, instance).Compile();

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
                compiledGetResultCall = Expression.Lambda<Func<object, object>>(getResultCall, awaiter).Compile();
            }

            // We search for an implicit interface implementation to prevent boxing for the case the type is a value type.
            var onCompletedMethod = awaiterType.GetMethod(nameof(INotifyCompletion.OnCompleted),
                                                          BindingFlags.Instance | BindingFlags.Public,
                                                          Type.DefaultBinder,
                                                          Type.EmptyTypes,
                                                          _emptyParameterModifiers);

            Action<object, Action> compiledOnCompletedCall;
            var continuationParameter = Expression.Parameter(typeof(Action), "continuation");

            if (onCompletedMethod == null)
            {
                onCompletedMethod = typeof(INotifyCompletion).GetMethod(nameof(INotifyCompletion.OnCompleted),
                                                                        BindingFlags.Instance | BindingFlags.Public,
                                                                        Type.DefaultBinder,
                                                                        Type.EmptyTypes,
                                                                        _emptyParameterModifiers);

                var convertedToInterfaceAwaiter = Expression.Convert(awaiter, typeof(INotifyCompletion));
                var onCompletedMethodCall = Expression.Call(convertedToInterfaceAwaiter, onCompletedMethod);
                compiledOnCompletedCall = Expression.Lambda<Action<object, Action>>(onCompletedMethodCall, awaiter, continuationParameter).Compile();
            }
            else
            {
                var onCompletedMethodCall = Expression.Call(convertedAwaiter, onCompletedMethod);
                compiledOnCompletedCall = Expression.Lambda<Action<object, Action>>(onCompletedMethodCall, awaiter, continuationParameter).Compile();
            }

            return new TypeDescriptor(type, resultType, awaiterType, compiledGetAwaiterCall, compiledIsCompletedPropertyAccess, compiledOnCompletedCall, compiledGetResultCall);
        }
    }
}
