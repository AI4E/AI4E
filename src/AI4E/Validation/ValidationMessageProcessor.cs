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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Handler;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Validation
{
    public class ValidationMessageProcessor : MessageProcessor
    {
        private readonly IServiceProvider _serviceProvider;

        public ValidationMessageProcessor(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public override async ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
        {
            if (!IsValidationEnabled(Context.MessageHandlerAction))
            {
                return await next(dispatchData);
            }

            var handledType = GetMessageHandlerMessageType(Context.MessageHandlerAction);

            do
            {
                if (TryGetDescriptor(Context.MessageHandler.GetType(), handledType, out var descriptor))
                {
                    var validationResults = await InvokeValidationAsync(
                        Context.MessageHandler,
                        descriptor,
                        dispatchData,
                        _serviceProvider,
                        cancellation);

                    if (validationResults.Any())
                    {
                        return new ValidationFailureDispatchResult(validationResults);
                    }

                    return await next(dispatchData);
                }

                handledType = handledType.BaseType;
            }
            while (handledType != null);

            throw new InvalidOperationException("No validation handlers found.");
        }

        #region TODO -  Move me to a separate type

        internal static async ValueTask<IEnumerable<ValidationResult>> InvokeValidationAsync(
            object messageHandler,
            ValidationDescriptor descriptor,
            DispatchDataDictionary dispatchData,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            var invoker = HandlerActionInvoker.GetInvoker(descriptor.Member);
            var returnTypeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(descriptor.Member.ReturnType);

            ValidationResultsBuilder validationResultsBuilder = null;

            object ResolveParameter(ParameterInfo parameter)
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    return serviceProvider;
                }
                else if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return cancellation;
                }
                else if (parameter.ParameterType == typeof(DispatchDataDictionary) ||
                         parameter.ParameterType == dispatchData.GetType())
                {
                    return dispatchData;
                }
                else if (parameter.ParameterType == typeof(ValidationResultsBuilder))
                {
                    if (validationResultsBuilder == null)
                    {
                        validationResultsBuilder = new ValidationResultsBuilder();
                    }

                    return validationResultsBuilder;
                }
                else if (parameter.HasDefaultValue)
                {
                    return serviceProvider.GetService(parameter.ParameterType) ?? parameter.DefaultValue;
                }
                else
                {
                    return serviceProvider.GetRequiredService(parameter.ParameterType);
                }
            }

            var result = await invoker.InvokeAsync(messageHandler, dispatchData.Message, ResolveParameter);

            if (returnTypeDescriptor.ResultType == typeof(void))
            {
                return validationResultsBuilder?.GetValidationResults() ?? Enumerable.Empty<ValidationResult>();
            }

            if (typeof(IEnumerable<ValidationResult>).IsAssignableFrom(returnTypeDescriptor.ResultType))
            {
                if (validationResultsBuilder == null)
                    return result as IEnumerable<ValidationResult> ?? Enumerable.Empty<ValidationResult>();

                if (result == null)
                    return validationResultsBuilder.GetValidationResults();

                return validationResultsBuilder.GetValidationResults().Concat(
                    (IEnumerable<ValidationResult>)result);
            }

            if (typeof(ValidationResult) == returnTypeDescriptor.ResultType)
            {
                var validationResult = (ValidationResult)result;

                if (validationResult == default)
                    return validationResultsBuilder?.GetValidationResults() ?? Enumerable.Empty<ValidationResult>();

                if (validationResultsBuilder == null)
                    return Enumerable.Repeat((ValidationResult)result, 1);

                return validationResultsBuilder.GetValidationResults().Concat(
                         Enumerable.Repeat((ValidationResult)result, 1));
            }

            if (typeof(ValidationResultsBuilder) == returnTypeDescriptor.ResultType)
            {
                if (result == validationResultsBuilder || result == null)
                    return validationResultsBuilder?.GetValidationResults() ?? Enumerable.Empty<ValidationResult>();

                if (validationResultsBuilder == null)
                    return ((ValidationResultsBuilder)result).GetValidationResults();

                return validationResultsBuilder.GetValidationResults().Concat(
                        ((ValidationResultsBuilder)result).GetValidationResults());
            }

            throw new InvalidOperationException();

        }

        private static readonly ConcurrentDictionary<Type, ImmutableDictionary<Type, ValidationDescriptor>> _descriptorsCache
            = new ConcurrentDictionary<Type, ImmutableDictionary<Type, ValidationDescriptor>>();

        internal static bool TryGetDescriptor(Type messageHandlerType, Type handledType, out ValidationDescriptor descriptor)
        {
            if (_descriptorsCache.TryGetValue(messageHandlerType, out var descriptors))
            {
                return descriptors.TryGetValue(handledType, out descriptor);
            }

            var members = ValidationInspector.Instance.InspectType(messageHandlerType);
            var duplicates = members.GroupBy(p => p.ParameterType).Where(p => p.Count() > 1);

            if (duplicates.Any(p => p.Key == handledType))
            {
                throw new InvalidOperationException("Ambigous validation");
            }

            var descriptorL = members.Where(p => p.ParameterType == handledType);
            var result = false;

            if (descriptorL.Any())
            {
                descriptor = descriptorL.First();
                result = true;
            }
            else
            {
                descriptor = default;
            }

            if (!duplicates.Any())
            {
                _descriptorsCache.TryAdd(messageHandlerType, members.ToImmutableDictionary(p => p.ParameterType));
            }

            return result;
        }

        internal static Type GetMessageHandlerMessageType(MessageHandlerActionDescriptor memberDescriptor)
        {
            return memberDescriptor.Member.GetParameters().First().ParameterType;
        }

        #endregion

        private static bool IsValidationEnabled(MessageHandlerActionDescriptor memberDescriptor)
        {
            var isValidationEnabled = memberDescriptor
                .MessageHandlerType
                .GetCustomAttribute<ValidateAttribute>(inherit: true)?.Validate ?? false;

            var attribute = memberDescriptor.Member.GetCustomAttribute<ValidateAttribute>(inherit: true);

            // If there is an attribute present in the inheritence hierarchy of the member but not on the
            // member itself, and there is a non-inherited attribute present on the type, use this.
            if (attribute != null &&
                memberDescriptor.Member.GetCustomAttribute<ValidateAttribute>(inherit: false) == null)
            {
                var nonInheritedTypeAttribute = memberDescriptor
                .MessageHandlerType
                .GetCustomAttribute<ValidateAttribute>(inherit: false);

                if (nonInheritedTypeAttribute != null)
                {
                    attribute = nonInheritedTypeAttribute;
                }
            }

            return attribute?.Validate ?? isValidationEnabled;
        }
    }
}
