/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;

namespace AI4E.Validation
{
    public class ValidationCommandProcessor : MessageProcessor
    {
        public override ValueTask<IDispatchResult> ProcessAsync<TMessage>(TMessage message, Func<TMessage, ValueTask<IDispatchResult>> next, CancellationToken cancellation)
        {
            var validationResultsBuilder = new ValidationResultsBuilder();
            var properties = typeof(TMessage).GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            foreach (var property in properties)
            {
                ValidateMember(validationResultsBuilder, property, property.GetValue(message));
            }

            for (var curr = typeof(TMessage); curr != typeof(object); curr = curr.BaseType)
            {
                var fields = curr.GetFields();

                foreach (var field in fields)
                {
                    ValidateMember(validationResultsBuilder, field, field.GetValue(message));
                }
            }

            var validationResults = validationResultsBuilder.GetValidationResults();

            if (validationResults.Any())
            {
                return new ValueTask<IDispatchResult>(new ValidationFailureDispatchResult(validationResults));
            }

            return next(message);
        }

        private static void ValidateMember(ValidationResultsBuilder validationResultsBuilder, MemberInfo member, object value)
        {
            var attributes = member.GetCustomAttributes<ValidateAttribute>(inherit: true);

            foreach (var attribute in attributes)
            {
                if (!attribute.Validate(value, out var message))
                {
                    validationResultsBuilder.AddValidationResult(member.Name, message);
                }
            }
        }
    }
}
