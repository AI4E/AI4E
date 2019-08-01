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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    /// <summary>
    /// Represents an action invoker that can invoke action methods.
    /// </summary>
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

        /// <summary>
        /// Gets the <see cref="HandlerActionInvoker"/> for the sepcfied method.
        /// </summary>
        /// <param name="methodInfo">The method.</param>
        /// <returns>The <see cref="HandlerActionInvoker"/> for <paramref name="methodInfo"/>.</returns>
        public static HandlerActionInvoker GetInvoker(MethodInfo methodInfo)
        {
            return _cache.GetOrAdd(methodInfo, _ => new HandlerActionInvoker(methodInfo));
        }

        private MethodInfo Method { get; }

        /// <summary>
        /// Gets the type of the actions first parameter.
        /// </summary>
        public Type FirstParameterType { get; }

        /// <summary>
        /// Gets the return type descriptor.
        /// </summary>
        public AwaitableTypeDescriptor ReturnTypeDescriptor { get; }

        /// <summary>
        /// Asynchronously invokes the action.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="argument">The argument for the first parameter.</param>
        /// <param name="parameterResolver">A parameter resolver used to resolve values for additional parameters.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the result that was returned from the action.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either of <paramref name="instance"/> or <paramref name="parameterResolver"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="instance"/> is not assignable to the actions declaring type is the type
        /// or <paramref name="argument"/> is not assignable to the actions first parameter type.
        /// </exception>
        public async ValueTask<object> InvokeAsync(object instance, object argument, Func<ParameterInfo, object> parameterResolver)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (parameterResolver == null)
                throw new ArgumentNullException(nameof(parameterResolver));

            if (!Method.DeclaringType.IsAssignableFrom(instance.GetType()))
                throw new ArgumentException($"The argument must be of type '{Method.DeclaringType.ToString()}' or an assignable type in order to be used as instance.", nameof(instance));

            if (FirstParameterType != typeof(void) && !(argument is null) && !FirstParameterType.IsAssignableFrom(argument.GetType()))
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
            return !type.IsValueType || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
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
