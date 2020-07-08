/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// Represents a notification recorder.
    /// </summary>
    /// <remarks>
    /// A notification recorder does not directly place a notification on the underlying notification manager but
    /// records them until published. On publish all recorded notifications are inserted into the underlying 
    /// notification manager.
    /// </remarks>
    public interface INotificationRecorder : INotificationManagerScope
    {
        /// <summary>
        /// Publishes all recorded notifications to the underlying notification manager.
        /// </summary>
        public void PublishNotifications();
    }
}
