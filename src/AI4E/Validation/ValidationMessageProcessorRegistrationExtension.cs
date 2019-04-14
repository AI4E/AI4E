using System.Reflection;
using AI4E.Validation;

namespace AI4E
{
    public static class ValidationMessageProcessorRegistrationExtension
    {
        public static bool CallOnValidation(this IMessageProcessorRegistration messageProcessorRegistration)
        {
            var processorType = messageProcessorRegistration.MessageProcessorType;

            var callOnValidationAttribute = processorType
                .GetCustomAttribute<CallOnValidationAttribute>(inherit: true);

            return callOnValidationAttribute != null && callOnValidationAttribute.CallOnValidation;
        }
    }
}
