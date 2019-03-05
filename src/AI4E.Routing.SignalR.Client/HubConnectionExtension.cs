using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

#if BLAZOR
using Blazor.Extensions;
#else
using Microsoft.AspNetCore.SignalR.Client;
#endif

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    public static partial class HubConnectionExtension
    {
        public static IDisposable Register<T>(this HubConnection hubConnection, T skeleton)
            where T : class
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            if (skeleton == null)
                throw new ArgumentNullException(nameof(skeleton));

            var methods = typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var disposables = new List<IDisposable>();
            foreach (var method in methods)
            {
                // We do not support generic methods
                if (method.ContainsGenericParameters)
                    continue;

                var parameters = method.GetParameters();

                // No ref, out, in, etc. parameters
                if (parameters.Any(p => p.ParameterType.IsByRef))
                    continue;

                if (method.DeclaringType != typeof(T))
                    continue;

#if BLAZOR
                var handlerRegistration = RegisterMethod(hubConnection, skeleton, method, parameters);
#else
                Func<object[], object, Task> handler;

                var args = Expression.Parameter(typeof(object[]), "args");
                var state = Expression.Parameter(typeof(object), "state");

                var convertedArgs = new Expression[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                {
                    var index = Expression.Constant(i, typeof(int));
                    var indexing = Expression.ArrayIndex(args, index);

                    convertedArgs[i] = Expression.Convert(indexing, parameters[i].ParameterType);
                }

                var methodCall = Expression.Call(Expression.Convert(state, typeof(T)), method, convertedArgs);

                // The method is asynchronous
                if (method.ReturnType.IsAssignableFrom(typeof(Task)))
                {
                    var convertedResult = Expression.Convert(methodCall, typeof(Task));

                    handler = Expression.Lambda<Func<object[], object, Task>>(convertedResult, args, state).Compile();
                }
                else
                {
                    var synchronousHandler = Expression.Lambda<Action<object[], object>>(methodCall, args, state).Compile();

                    handler = (argsX, stateX) =>
                    {
                        synchronousHandler(argsX, stateX);
                        return Task.CompletedTask;
                    };
                }

                var handlerRegistration = hubConnection.On(method.Name, parameters.Select(p => p.ParameterType).ToArray(), handler, state: skeleton);
#endif
                disposables.Add(handlerRegistration);
            }

            return new CombinedDisposable(disposables);
        }

        public static Task InvokeAsync<T>(this HubConnection hubConnection, Expression<Func<T, Task>> expression, CancellationToken cancellation = default)
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body is MethodCallExpression methodCallExpression)
            {
                var method = methodCallExpression.Method;
                var arguments = methodCallExpression.Arguments.Select(p => GetExpressionValue(p)).ToArray();

#if BLAZOR
                return hubConnection.InvokeAsync(method.Name, arguments);
#else
                return hubConnection.SendCoreAsync(method.Name, arguments, cancellation);
#endif
            }

            throw new NotSupportedException();
        }

        public static async Task<TResult> InvokeAsync<T, TResult>(this HubConnection hubConnection, Expression<Func<T, Task<TResult>>> expression, CancellationToken cancellation = default)
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body is MethodCallExpression methodCallExpression)
            {
                var method = methodCallExpression.Method;
                var arguments = methodCallExpression.Arguments.Select(p => GetExpressionValue(p)).ToArray();

                TResult result;

#if BLAZOR
                result = await hubConnection.InvokeAsync<TResult>(method.Name, arguments);
#else
                var objResult = await hubConnection.InvokeCoreAsync(method.Name, typeof(TResult), arguments, cancellation);

                if(objResult != null)
                {
                    result = (TResult)objResult;
                }
                else
                {
                    result = default;
                }
#endif
                return result;
            }

            throw new NotSupportedException();
        }

        // TODO: Duplicate (see ProxyHost)
        private static object GetExpressionValue(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is FieldInfo field &&
                    memberExpression.Expression is ConstantExpression fieldOwner)
                {
                    return field.GetValue(fieldOwner.Value);
                }

                // TODO
            }

            var valueFactory = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile();

            return valueFactory();
        }

        private sealed class CombinedDisposable : IDisposable
        {
            private readonly IEnumerable<IDisposable> _disposables;

            public CombinedDisposable(IEnumerable<IDisposable> disposables)
            {
                Assert(disposables != null);
                Assert(!disposables.Any(p => p == null));
                _disposables = disposables;
            }

            public void Dispose()
            {
                foreach (var disposable in _disposables)
                    disposable.Dispose();
            }
        }
    }
}
