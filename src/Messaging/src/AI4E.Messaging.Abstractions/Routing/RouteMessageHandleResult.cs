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

namespace AI4E.Messaging.Routing
{
    /// <summary>
    /// Represents the result of handling a request route message.
    /// </summary>
    public readonly struct RouteMessageHandleResult
    {
        /// <summary>
        /// Creates a new instance of the <see cref="RouteMessageHandleResult"/> type.
        /// </summary>
        /// <param name="routeMessage">
        /// The message that is the response generated as result of handling the request.
        /// </param>
        /// <param name="handled">
        /// A boolean value indicating whether the request message was handled succesfully.
        /// </param>
        public RouteMessageHandleResult(RouteMessage<IDispatchResult> routeMessage, bool handled)
        {
            RouteMessage = routeMessage;
            Handled = handled;
        }

        /// <summary>
        /// Gets the message that is the response generated as result of handling the request.
        /// </summary>
        public RouteMessage<IDispatchResult> RouteMessage { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the request message was handled succesfully 
        /// in the context of the message routing layer.
        /// </summary>
        /// <remarks>
        /// If this is true, this does not imply that the <see cref="IDispatchResult"/> that is encoded in
        /// <see cref="RouteMessage"/> is a success. This flag only indicates that a message-dispatcher was able
        /// to dispatch the request message to one or multiple message handlers successfully.
        /// </remarks>
        public bool Handled { get; }
    }
}
