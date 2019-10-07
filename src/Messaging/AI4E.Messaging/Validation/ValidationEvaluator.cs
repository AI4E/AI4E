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
using AI4E.Messaging.MessageHandlers;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.Validation
{
    internal static class ValidationEvaluator
    {
        public static async ValueTask<IEnumerable<ValidationResult>> EvaluateAsync(
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
    }
}
