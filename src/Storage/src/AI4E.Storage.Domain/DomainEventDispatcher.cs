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

using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IDomainEventDispatcher"/>
    public sealed class DomainEventDispatcher : IDomainEventDispatcher
    {
#pragma warning disable CA2213
        private readonly IMessageDispatcher _messageDispatcher;
#pragma warning restore CA2213
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IOptions<DomainStorageOptions> _optionsAccessor;
        private readonly ILogger<DomainEventDispatcher> _logger;
        private readonly AsyncDisposeHelper _disposeHelper = new AsyncDisposeHelper();

        /// <summary>
        /// Creates a new instance of the <see cref="DomainEventDispatcher"/> type.
        /// </summary>
        /// <param name="messageDispatcher">The <see cref="IMessageDispatcher"/> used to dispatch messages.</param>
        /// <param name="dateTimeProvider">The <see cref="IDateTimeProvider"/> for date and time access.</param>
        /// <param name="optionsAccessor">
        /// An <see cref="IOptions{DomainStorageOptions}"/> that is used to resolve domain storage options.
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger{DomainEventDispatcher}"/> used for logging or <c>null</c> to disable logging.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageDispatcher"/>, <paramref name="dateTimeProvider"/>
        /// or <paramref name="optionsAccessor"/> is <c>null</c>.
        /// </exception>
        public DomainEventDispatcher(
            IMessagingEngine messagingEngine,
            IDateTimeProvider dateTimeProvider,
            IOptions<DomainStorageOptions> optionsAccessor,
            ILogger<DomainEventDispatcher>? logger = null)
        {
            if (messagingEngine is null)
                throw new ArgumentNullException(nameof(messagingEngine));

            if (dateTimeProvider is null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (optionsAccessor is null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _messageDispatcher = messagingEngine.CreateDispatcher();
            _dateTimeProvider = dateTimeProvider;
            _optionsAccessor = optionsAccessor;
            _logger = logger ?? NullLogger<DomainEventDispatcher>.Instance;
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        /// <inheritdoc/>
        public async ValueTask DispatchAsync(DomainEvent domainEvent, CancellationToken cancellation)
        {
            using var disposalGuard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
            cancellation = disposalGuard.Cancellation;

            try
            {
                do
                {
                    try
                    {
                        var dispatchData = DispatchDataDictionary.Create(domainEvent.EventType, domainEvent.Event);
                        var currentDelay = _optionsAccessor.Value.InitalDispatchFailureDelay;

                        _logger.LogDebug(
                            Resources.DispatchingDomainEvent,
                            domainEvent.EventType,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);


                        while (!await TryDispatchAsync(dispatchData, cancellation).ConfigureAwait(false))
                        {
                            await _dateTimeProvider.DelayAsync(currentDelay, cancellation).ConfigureAwait(false);

                            currentDelay += currentDelay;
                            currentDelay = currentDelay > _optionsAccessor.Value.MaxDispatchFailureDelay
                                ? _optionsAccessor.Value.MaxDispatchFailureDelay
                                : currentDelay;
                        }


                        _logger.LogDebug(
                            Resources.SuccessfullyDispatchedDomainEvent,
                            domainEvent.EventType,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);


                        break;
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
#pragma warning disable CA1031
                    catch (Exception exc)
#pragma warning restore CA1031
                    {
                        _logger.LogWarning(
                            exc,
                            Resources.ExceptionDispatchingDomainEvent,
                            domainEvent.EventType,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);

                    }
                }
                while (true);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private async ValueTask<bool> TryDispatchAsync(
            DispatchDataDictionary dispatchData,
            CancellationToken cancellation)
        {
            var dispatchResult = await _messageDispatcher.DispatchLocalAsync(dispatchData, publish: true, cancellation)
                .ConfigureAwait(false);

            if (!dispatchResult.IsSuccess)
            {
                _logger.LogWarning(
                    Resources.FailureDispatchingDomainEvent,
                    dispatchData.MessageType,
                    dispatchResult.Message,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);
            }

            return dispatchResult.IsSuccess;
        }

        #region Registration

        internal static void Register(IDomainStorageBuilder domainStorageBuilder)
        {
            var services = domainStorageBuilder.Services;

            services.AddSingleton<DomainEventDispatcher>();
            services.AddSingleton<IDomainEventDispatcher>(
                serviceProvider => serviceProvider.GetRequiredService<DomainEventDispatcher>());
        }

        #endregion

        /// <inheritdoc/>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        private ValueTask DisposeInternalAsync()
        {
            return _messageDispatcher.DisposeAsync();
        }
    }
}
