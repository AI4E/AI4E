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
using System.Collections.Generic;
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

        public override async ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
        {
            if (!IsValidationEnabled(Context.MessageHandlerAction))
            {
                return await next(dispatchData);
            }

            var members = ValidationInspector.Instance.InspectType(Context.MessageHandler.GetType());
            var handledType = GetMessageHandlerMessageType(Context.MessageHandlerAction);

            do
            {
                var ofType = members.Where(p => p.ParameterType == handledType);

                if (ofType.Any())
                {
                    if (ofType.Skip(1).Any())
                    {
                        throw new InvalidOperationException("Ambigous validation");
                    }

                    var descriptor = ofType.First();

                    var validationResults = await InvokeValidationAsync(descriptor, dispatchData, cancellation);

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

        private async ValueTask<IEnumerable<ValidationResult>> InvokeValidationAsync<TMessage>(
            ValidationDescriptor descriptor,
            DispatchDataDictionary<TMessage> dispatchData,
            CancellationToken cancellation)
            where TMessage : class
        {
            var invoker = HandlerActionInvoker.GetInvoker(descriptor.Member);
            var returnTypeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(descriptor.Member.ReturnType);

            ValidationResultsBuilder validationResultsBuilder = null;

            object ResolveParameter(ParameterInfo parameter)
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    return _serviceProvider;
                }
                else if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return cancellation;
                }
                else if (parameter.ParameterType == typeof(DispatchDataDictionary) ||
                         parameter.ParameterType == typeof(DispatchDataDictionary<TMessage>))
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
                    return _serviceProvider.GetService(parameter.ParameterType) ?? parameter.DefaultValue;
                }
                else
                {
                    return _serviceProvider.GetRequiredService(parameter.ParameterType);
                }
            }

            var result = await invoker.InvokeAsync(Context.MessageHandler, dispatchData.Message, ResolveParameter);

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

        private static Type GetMessageHandlerMessageType(MessageHandlerActionDescriptor memberDescriptor)
        {
            return memberDescriptor.Member.GetParameters().First().ParameterType;
        }

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
