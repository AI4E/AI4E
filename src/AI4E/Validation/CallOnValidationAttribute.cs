using System;

namespace AI4E.Validation
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed class CallOnValidationAttribute : Attribute
    {
        public CallOnValidationAttribute() : this(true) { }

        public CallOnValidationAttribute(bool callOnValidation)
        {
            CallOnValidation = callOnValidation;
        }

        public bool CallOnValidation { get; }
    }
}
