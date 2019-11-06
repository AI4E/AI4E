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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * ASP.Net Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

namespace System.Reflection
{
    public static class AI4EUtilsMethodInfoExtensions
    {
        // This version of MethodInfo.Invoke removes TargetInvocationExceptions
        public static object? InvokeWithoutWrappingExceptions(
#pragma warning disable CA1720
            this MethodInfo methodInfo, object? obj, object?[]? parameters)
#pragma warning restore CA1720
        {
            // These are the default arguments passed when methodInfo.Invoke(obj, parameters) are called. We do the same
            // here but specify BindingFlags.DoNotWrapExceptions to avoid getting TAE (TargetInvocationException)
            // methodInfo.Invoke(obj, BindingFlags.Default, binder: null, parameters: parameters, culture: null)

#if NETSTD20
            try
            {
#pragma warning disable CA1062
                return methodInfo.Invoke(
                    obj, BindingFlags.Default, binder: null, parameters: parameters, culture: null);
#pragma warning restore CA1062
            }
            catch (TargetInvocationException exc)
            {
                throw exc.InnerException;
            }
#else
#pragma warning disable CA1062
            return methodInfo.Invoke(
                obj, BindingFlags.DoNotWrapExceptions, binder: null, parameters: parameters, culture: null);
#pragma warning restore CA1062
               
#endif
        }
    }
}
