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

namespace AI4E.Validation
{
    /// <summary>
    /// Instructs the messaging system to call the decorated message processors on validation dispatches.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed class CallOnValidationAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of the <see cref="CallOnValidationAttribute"/> type.
        /// </summary>
        public CallOnValidationAttribute() : this(true) { }

        /// <summary>
        /// Creates a new instance of the <see cref="CallOnValidationAttribute"/> type.
        /// </summary>
        /// <param name="callOnValidation">
        /// A boolean value indicating whether the decorated processor shall be called on validation dispatches.
        /// </param>
        public CallOnValidationAttribute(bool callOnValidation)
        {
            CallOnValidation = callOnValidation;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the decorated processor shall be called on validation dispatches.
        /// </summary>
        public bool CallOnValidation { get; }
    }
}
