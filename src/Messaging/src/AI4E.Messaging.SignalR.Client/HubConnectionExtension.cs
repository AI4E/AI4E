using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace AI4E.Messaging.SignalR.Client
{
    public static class HubConnectionExtension
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
                var arguments = methodCallExpression.Arguments.Select(p => p.Evaluate()).ToArray();

                return hubConnection.SendCoreAsync(method.Name, arguments, cancellation);
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
                var arguments = methodCallExpression.Arguments.Select(p => p.Evaluate()).ToArray();

                TResult result;

                var objResult = await hubConnection.InvokeCoreAsync(method.Name, typeof(TResult), arguments, cancellation);

                if (objResult != null)
                {
                    result = (TResult)objResult;
                }
                else
                {
                    result = default;
                }

                return result;
            }

            throw new NotSupportedException();
        }

        private sealed class CombinedDisposable : IDisposable
        {
            private readonly IEnumerable<IDisposable> _disposables;

            public CombinedDisposable(IEnumerable<IDisposable> disposables)
            {
                Debug.Assert(disposables != null);
                Debug.Assert(!disposables.Any(p => p == null));
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
