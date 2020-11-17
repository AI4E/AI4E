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

using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public sealed class Issue241
    {
        private static IMessageDispatcher BuildMessageDispatcher(ObjectHandler handler)
        {
            return MessagingBuilder.CreateDefault().ConfigureMessageHandlers((registry, serviceProvider) =>
            {
                registry.Register(new MessageHandlerRegistration<object>(serviceProvider => handler));
            }).BuildMessageDispatcher();
        }

        [TestMethod]
        public async Task Test()
        {
            var handler = new ObjectHandler();
            var messageDispatcher = BuildMessageDispatcher(handler);

            await messageDispatcher.DispatchAsync(new DispatchDataDictionary<string>("abc"), cancellation: default);

            Assert.AreEqual(typeof(string), handler.DispatchData.MessageType);
        }

        private sealed class ObjectHandler : IMessageHandler<object>
        {
            public DispatchDataDictionary<object> DispatchData { get; set; }

            public ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary<object> dispatchData,
                bool publish,
                bool localDispatch,
                RouteEndPointScope remoteScope,
                CancellationToken cancellation)
            {
                DispatchData = dispatchData;
                return new ValueTask<IDispatchResult>(new SuccessDispatchResult());
            }
        }
    }
}
