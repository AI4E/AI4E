/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IActivationNotifyable.cs 
 * Types:           AI4E.IActivationNotifyable
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   29.04.2017 
 * Status:          Ready
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

using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// An object whose activation can be notified asynchronously.
    /// </summary>
    public interface IActivationNotifyable
    {
        /// <summary>
        /// Asynchronously notifies the objects activation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifyActivationAsync();
    }
}
