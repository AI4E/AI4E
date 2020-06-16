using System;
using System.ComponentModel;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.SignalR.Client
{
    // Adapted from https://github.com/aspnet/SignalR/blob/dev/src/Microsoft.AspNetCore.SignalR.Client.Core/HubConnectionBuilder.cs

    /// <summary>
    /// A builder for configuring <see cref="HubConnection"/> instances.
    /// </summary>
    public class HubConnectionBuilder : IHubConnectionBuilder
    {
        private bool _hubConnectionBuilt;

        /// <inheritdoc />
        public IServiceCollection Services { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HubConnectionBuilder"/> class.
        /// </summary>
        public HubConnectionBuilder(IServiceCollection services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            Services = services;
            Services.AddSingleton<HubConnection>();
            Services.AddLogging();
            this.AddJsonProtocol();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HubConnectionBuilder"/> class.
        /// </summary>
        public HubConnectionBuilder() : this(new ServiceCollection()) { }

        /// <inheritdoc />
        public HubConnection Build()
        {
            // Build can only be used once
            if (_hubConnectionBuilt)
            {
                throw new InvalidOperationException("HubConnectionBuilder allows creation only of a single instance of HubConnection.");
            }

            _hubConnectionBuilt = true;

            // The service provider is disposed by the HubConnection
            var serviceProvider = Services.BuildServiceProvider();

            var connectionFactory = serviceProvider.GetService<IConnectionFactory>() ??
                throw new InvalidOperationException($"Cannot create {nameof(HubConnection)} instance. An {nameof(IConnectionFactory)} was not configured.");

            var endPoint = serviceProvider.GetService<EndPoint>() ??
                throw new InvalidOperationException($"Cannot create {nameof(HubConnection)} instance. An {nameof(EndPoint)} was not configured.");

            return serviceProvider.GetService<HubConnection>();
        }

        // Prevents from being displayed in intellisense
        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        // Prevents from being displayed in intellisense
        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        // Prevents from being displayed in intellisense
        /// <inheritdoc />
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return base.ToString();
        }

        // Prevents from being displayed in intellisense
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Type GetType()
        {
            return base.GetType();
        }
    }
}
