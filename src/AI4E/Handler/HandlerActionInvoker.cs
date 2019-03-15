using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class HandlerActionInvoker
    {
        private static readonly ConcurrentDictionary<MethodInfo, HandlerActionInvoker> _cache = new ConcurrentDictionary<MethodInfo, HandlerActionInvoker>();
        private readonly Func<object, object, Func<ParameterInfo, object>, object> _invoker;

        private HandlerActionInvoker(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (method.IsGenericMethodDefinition)
                throw new ArgumentException("The specified method must not be a generic type definition.", nameof(method));

            Method = method;
            ReturnTypeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(method.ReturnType);
            var firstParameterType = method.GetParameters().Select(p => p.ParameterType).FirstOrDefault() ?? typeof(void);
            _invoker = BuildInvoker(method, firstParameterType);
            FirstParameterType = firstParameterType;
        }

        public static HandlerActionInvoker GetInvoker(MethodInfo methodInfo)
        {
            return _cache.GetOrAdd(methodInfo, _ => new HandlerActionInvoker(methodInfo));
        }

        private MethodInfo Method { get; }

        public Type FirstParameterType { get; }

        public AwaitableTypeDescriptor ReturnTypeDescriptor { get; }

        public async ValueTask<object> InvokeAsync(object instance, object argument, Func<ParameterInfo, object> parameterResolver)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (FirstParameterType != typeof(void) && argument == null)
                throw new ArgumentNullException(nameof(argument));

            if (parameterResolver == null)
                throw new ArgumentNullException(nameof(parameterResolver));

            if (!Method.DeclaringType.IsAssignableFrom(instance.GetType()))
                throw new ArgumentException($"The argument must be of type '{Method.DeclaringType.ToString()}' or an assignable type in order to be used as instance.", nameof(instance));

            if (FirstParameterType != typeof(void) && !FirstParameterType.IsAssignableFrom(argument.GetType()))
                throw new ArgumentException($"The argument must be of type '{FirstParameterType}' or an assignable type in order to be used as first argument.", nameof(argument));

            var result = _invoker(instance, argument, parameterResolver);

            if (ReturnTypeDescriptor.IsAwaitable)
            {
                Assert(result != null);

                result = await ReturnTypeDescriptor.GetAwaitable(result);
            }

            AssertLegalResult(result);
            return result;
        }

        private void AssertLegalResult(object result)
        {
            var returnType = ReturnTypeDescriptor.ResultType;
            Assert(result == null && (IsNullable(returnType) || returnType == typeof(void)) ||
                   result != null && returnType.IsAssignableFrom(result.GetType()));
        }

        private static bool IsNullable(Type type)
        {
            return type.IsClass || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static object ResolveParameter(Func<ParameterInfo, object> parameterResolver, ParameterInfo parameter)
        {
            return parameterResolver(parameter);
        }

        // Func<object,                     -> Instance
        //     object,                      -> First argument
        //     Func<ParameterInfo, object>, -> Parameter resolver
        //     object>                      -> Return value (null in case of void result)
        private static Func<object, object, Func<ParameterInfo, object>, object> BuildInvoker(MethodInfo method, Type firstParameterType)
        {
            var instance = Expression.Parameter(typeof(object), "instance");
            var convertedInstance = Expression.Convert(instance, method.DeclaringType);

            if (firstParameterType == typeof(void))
            {
                var methodCall = Expression.Call(convertedInstance, method);

                if (method.ReturnType == typeof(void))
                {
                    var expression = Expression.Lambda<Action<object>>(methodCall, instance);
                    var compiledExpression = expression.Compile();

                    return (obj, firstArg, paramResolver) => { compiledExpression(obj); return null; };
                }
                else
                {
                    var expression = Expression.Lambda<Func<object, object>>(Expression.Convert(methodCall, typeof(object)), instance);
                    var compiledExpression = expression.Compile();

                    return (obj, firstArg, paramResolver) => compiledExpression(obj);
                }
            }
            else
            {
                var parameters = method.GetParameters();
                var resolveParameterMethod = typeof(HandlerActionInvoker).GetMethod(nameof(ResolveParameter), BindingFlags.Static | BindingFlags.NonPublic);

                var firstArgument = Expression.Parameter(typeof(object), "firstArgument");
                var convertedFirstArgument = Expression.Convert(firstArgument, firstParameterType);
                var parameterResolver = Expression.Parameter(typeof(Func<ParameterInfo, object>), "parameterResolver");

                var arguments = new Expression[parameters.Length];
                arguments[0] = convertedFirstArgument;

                for (var i = 1; i < arguments.Length; i++)
                {
                    var parameter = Expression.Constant(parameters[i], typeof(ParameterInfo));
                    var argument = Expression.Convert(Expression.Call(resolveParameterMethod, parameterResolver, parameter), parameters[i].ParameterType);

                    arguments[i] = argument;
                }

                var methodCall = Expression.Call(convertedInstance, method, arguments);

                if (method.ReturnType == typeof(void))
                {
                    var expression = Expression.Lambda<Action<object, object, Func<ParameterInfo, object>>>(methodCall, instance, firstArgument, parameterResolver);
                    var compiledExpression = expression.Compile();

                    return (obj, firstArg, paramResolver) => { compiledExpression(obj, firstArg, paramResolver); return null; };
                }
                else
                {
                    var expression = Expression.Lambda<Func<object, object, Func<ParameterInfo, object>, object>>(Expression.Convert(methodCall, typeof(object)), instance, firstArgument, parameterResolver);
                    return expression.Compile();
                }
            }
        }
    }
}
