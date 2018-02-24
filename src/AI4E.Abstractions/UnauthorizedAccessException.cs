/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        UnauthorizedAccessException.cs 
 * Types:           AI4E.UnauthorizedAccessException
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   01.06.2017 
 * Status:          In development
 * --------------------------------------------------------------------------------------------------------------------
 */

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

namespace AI4E
{
    /// <summary>
    /// Represents a type of exception that is thrown if access to an operation is unauthorized.
    /// </summary>
    public class UnauthorizedAccessException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="UnauthorizedAccessException"/> type.
        /// </summary>
        public UnauthorizedAccessException() : base() { }
    }
}
