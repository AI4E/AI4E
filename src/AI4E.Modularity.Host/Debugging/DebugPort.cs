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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using JsonRpc.Streams;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugPort
    {
        private readonly TcpListener _tcpHost;
        private readonly IAsyncProcess _connectionProcess;
        private readonly ConcurrentDictionary<DebugSession, byte> _debugSessions = new ConcurrentDictionary<DebugSession, byte>();
        private readonly IEndPointManager _endPointManager;
        private readonly IRouteStore _routeStore;
        private readonly IMessageDispatcher _messageDispatcher;

        public DebugPort(IEndPointManager endPointManager, IRouteStore routeStore, IMessageDispatcher messageDispatcher)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));
            if (routeStore == null)
                throw new ArgumentNullException(nameof(routeStore));
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));
            _connectionProcess = new AsyncProcess(ConnectProcedure);
            _tcpHost = new TcpListener(new IPEndPoint(IPAddress.Loopback, 8080));
            _tcpHost.Start();
            LocalAddress = (IPEndPoint)_tcpHost.Server.LocalEndPoint;
            Assert(LocalAddress != null);

            _connectionProcess.Start();
            _endPointManager = endPointManager;
            _routeStore = routeStore;
            _messageDispatcher = messageDispatcher;
        }

        public IPEndPoint LocalAddress { get; }

        private async Task ConnectProcedure(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var client = await _tcpHost.AcceptTcpClientAsync().WithCancellation(cancellation);
                    var stream = client.GetStream();

                    _debugSessions.TryAdd(new DebugSession(this, stream, _endPointManager, _routeStore, _messageDispatcher), 0);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exc)
                {
                    // TODO: Log exception
                }
            }
        }

        private sealed class DebugSession : IDisposable, IDebugSession
        {
            private static readonly IJsonRpcContractResolver myContractResolver = new JsonRpcContractResolver
            {
                // Use camelcase for RPC method names.
                NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
                // Use camelcase for the property names in parameter value objects
                ParameterValueConverter = new CamelCaseJsonValueConverter()
            };

            private readonly DebugPort _debugPort;
            private readonly Stream _stream;
            private readonly IEndPointManager _endPointManager;
            private readonly IRouteStore _routeStore;
            private readonly IMessageDispatcher _messageDispatcher;
            private readonly ByLineTextMessageReader _reader;
            private readonly ByLineTextMessageWriter _writer;
            private readonly IDisposable _attachment;
            private EndPointRouter _endPointRouter;

            private DateTime _lease;
            private readonly IAsyncProcess _leaseProcess;
            private readonly object _lock = new object();

            public DebugSession(DebugPort debugPort,
                                Stream stream,
                                IEndPointManager endPointManager,
                                IRouteStore routeStore,
                                IMessageDispatcher messageDispatcher)
            {
                if (debugPort == null)
                    throw new ArgumentNullException(nameof(debugPort));

                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));

                if (endPointManager == null)
                    throw new ArgumentNullException(nameof(endPointManager));

                if (routeStore == null)
                    throw new ArgumentNullException(nameof(routeStore));
                if (messageDispatcher == null)
                    throw new ArgumentNullException(nameof(messageDispatcher));
                _debugPort = debugPort;
                _stream = stream;
                _endPointManager = endPointManager;
                _routeStore = routeStore;
                _messageDispatcher = messageDispatcher;
                _leaseProcess = new AsyncProcess(LeaseProcess);
                var host = BuildServiceHost();
                var serverHandler = new StreamRpcServerHandler(host);

                serverHandler.DefaultFeatures.Set<IDebugSession>(this);

                _reader = new ByLineTextMessageReader(stream);
                _writer = new ByLineTextMessageWriter(stream);
                _attachment = serverHandler.Attach(_reader, _writer);
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

                        LeaseEnd();
                        Assert(cancellation.IsCancellationRequested);
                        return;
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception exc)
                    {
                        // TODO: Log exception
                    }
                }
            }

            public void Dispose()
            {
                _leaseProcess.Terminate();
                _attachment.Dispose();
                _writer.Dispose();
                _reader.Dispose();


                _debugPort._debugSessions.TryRemove(this, out _);

                if (_endPointRouter != null)
                {
                    _messageDispatcher.DispatchAsync(new EndPointDisconnected(_endPointRouter.LocalEndPoint)).GetAwaiter().GetResult(); // TODO

                    foreach(var registration in _registrations)
                    {
                        _endPointRouter.UnregisterRouteAsync(registration, CancellationToken.None).GetAwaiter().GetResult(); // TODO
                    }

                    _endPointRouter.Dispose();
                }
            }

            private IJsonRpcServiceHost BuildServiceHost()
            {
                var builder = new JsonRpcServiceHostBuilder
                {
                    ContractResolver = myContractResolver,
                };
                // Register all the services (public classes) found in the assembly
                builder.Register(GetType().Assembly);
                // Add a middleware to log the requests and responses
                builder.Intercept(async (context, next) =>
                {
                    Console.WriteLine("> {0}", context.Request);
                    await next();
                    Console.WriteLine("< {0}", context.Response);
                });
                return builder.Build();
            }

            public void RenewLease()
            {
                lock (_lock)
                {
                    _lease = DateTime.Now + TimeSpan.FromSeconds(20);
                }
            }

            private void LeaseEnd()
            {
                Console.WriteLine($"Lease for debug session {_endPointRouter?.LocalEndPoint} retired. Closing connection...");
                Dispose();
            }

            public IEndPointRouter GetEndPointRouter()
            {
                if (_endPointRouter == null)
                    throw new InvalidOperationException();

                RenewLease();

                return _endPointRouter;
            }

            public void Init(EndPointRoute endPointRoute)
            {
                if (_endPointRouter != null)
                    throw new InvalidOperationException();

                RenewLease();
                _leaseProcess.Start();
                _messageDispatcher.DispatchAsync(new EndPointConnected(endPointRoute)).GetAwaiter().GetResult(); // TODO

                _endPointRouter = new EndPointRouter(_endPointManager, _routeStore, endPointRoute);
            }

            private readonly HashSet<string> _registrations = new HashSet<string>();

            public void RegisterRoute(string messageType)
            {
                _registrations.Add(messageType);
            }

            public void UnregisterRoute(string messageType)
            {
                _registrations.Remove(messageType);
            }
        }
    }
}
