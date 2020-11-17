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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace AI4E.Utils
{
    public sealed class ExceptionHelper
    {
        public static void HandleExceptions(Action action, ILogger? logger = null)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                action();
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                if (logger != null)
                {
                    logger.LogError(exc, "An exception occured unexpectedly.");
                }
                else
                {
                    Debug.WriteLine("An exception occured unexpectedly.");
                    Debug.WriteLine(exc.ToString());
                }
            }
        }

        [return: MaybeNull]
        [return: NotNullIfNotNull("defaultValue")]
        public static T HandleExceptions<T>(Func<T> func, ILogger? logger = null, [MaybeNull] T defaultValue = default)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));

            try
            {
                return func();
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                LogException(exc, logger);
            }

            return defaultValue;
        }

        public static void LogException(Exception exc, ILogger? logger = null)
        {
            if (exc is null)
                throw new ArgumentNullException(nameof(exc));

            if (logger != null)
            {
                logger.LogError(exc, "An exception occured unexpectedly");
            }
            else
            {
                Debug.WriteLine("An exception occured unexpectedly.");
                Debug.WriteLine(exc.ToString());
            }
        }
    }
}
