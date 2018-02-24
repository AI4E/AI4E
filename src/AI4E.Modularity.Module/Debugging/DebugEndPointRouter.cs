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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Nito.AsyncEx;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugEndPointRouter : IEndPointRouter, IDisposable
    {
        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();

        private readonly IEndPointRouterStub _stub;
        private readonly Task _initialization;

        private DateTime _lease;
        private readonly IAsyncProcess _leaseProcess;
        private readonly object _lock = new object();

        public DebugEndPointRouter(EndPointRoute localEndPoint, IEndPointRouterStub stub)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (stub == null)
                throw new ArgumentNullException(nameof(stub));

            LocalEndPoint = localEndPoint;
            _stub = stub;
            _leaseProcess = new AsyncProcess(LeaseProcess);
            _initialization = _stub.Init(localEndPoint.ToString());

            _leaseProcess.StartExecution();
        }

        private async Task LeaseProcess(CancellationToken cancellation)
        {
            var lease = default(DateTime);

            lock (_lock)
            {
                lease = _lease;
            }

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var now = DateTime.Now;

                    if (lease - now >= TimeSpan.Zero)
                    {
                        await Task.Delay(lease - now);
                    }

                    var newLease = default(DateTime);

                    lock (_lock)
                    {
                        newLease = _lease;
                    }

                    if (newLease != lease)
                    {
                        lease = newLease;
                        continue;
                    }

                    if (DateTime.Now < lease)
                    {
                        continue;
                    }

                    await _stub.RenewLease(LocalEndPoint.ToString());
                    RenewLease();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exc)
                {
                    // TODO: Log exception
                }
            }
        }

        private void RenewLease()
        {
            lock (_lock)
            {
                _lease = DateTime.Now + TimeSpan.FromSeconds(10);
            }
        }

        public EndPointRoute LocalEndPoint { get; }

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            await _initialization;

            var buffer = await _stub.ReceiveAsync(LocalEndPoint.ToString());
            RenewLease();

            using (var stream = new MemoryStream(buffer))
            {
                var message = new Message();
                await message.ReadAsync(stream, cancellation: default);
                return message;
            }
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            await _initialization;
            var result = await _stub.GetRoutesAsync(LocalEndPoint.ToString(), messageType);
            RenewLease();
            return result.Select(p => EndPointRoute.CreateRoute(p));
        }

        public async Task RegisterRouteAsync(string messageType, CancellationToken cancellation)
        {
            await _initialization;
            await _stub.RegisterRouteAsync(LocalEndPoint.ToString(), messageType);
            RenewLease();
        }

        public async Task SendAsync(IMessage message, EndPointRoute route, CancellationToken cancellation)
        {
            await _initialization;
            var buffer = new byte[message.Length];

            using (var stream = new MemoryStream(buffer, writable: true))
            {
                await message.WriteAsync(stream, cancellation);
            }

            await _stub.SendAsync(LocalEndPoint.ToString(), buffer, route.ToString());
            RenewLease();
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            await _initialization;
            var buffer1 = new byte[response.Length];

            using (var stream = new MemoryStream(buffer1, writable: true))
            {
                await response.WriteAsync(stream, cancellation);
            }

            var buffer2 = new byte[request.Length];

            using (var stream = new MemoryStream(buffer2, writable: true))
            {
                await request.WriteAsync(stream, cancellation);
            }

            await _stub.SendAsync(LocalEndPoint.ToString(), buffer1, buffer2);
            RenewLease();
        }

        public async Task UnregisterRouteAsync(string messageType, CancellationToken cancellation)
        {
            await _initialization;
            await _stub.UnregisterRouteAsync(LocalEndPoint.ToString(), messageType);
            RenewLease();
        }

        public void Dispose()
        {
            _leaseProcess.TerminateExecution();
        }
    }
}
