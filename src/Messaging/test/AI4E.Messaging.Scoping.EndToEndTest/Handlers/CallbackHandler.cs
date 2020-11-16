/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using AI4E.Messaging.Scoping.EndToEndTest.Messages;
using AI4E.Messaging.Scoping.EndToEndTest.Services;

namespace AI4E.Messaging.Scoping.EndToEndTest.Handlers
{
    public sealed class CallbackHandler
    {
        private readonly ScopedService _scopedService;

        public CallbackHandler(ScopedService scopedService)
        {
            if (scopedService is null)
                throw new ArgumentNullException(nameof(scopedService));

            _scopedService = scopedService;
        }

        // This handler should be called within a scoped message dispatcher, so we resolve the scoped service from it,
        // register the service to the global assertion lookup, so that we can check whether the handler was
        // called within the correct scope.
        public void Handle(CallbackCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            _scopedService.SetHandled();
        }
    }
}
