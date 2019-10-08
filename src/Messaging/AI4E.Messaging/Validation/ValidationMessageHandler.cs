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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI4E.Messaging.Validation
{
    [NoMessageHandler] // Prevent the messaging system to register this as ordinary handler, as we register this ourselves.
    public sealed class ValidationMessageHandler<TValidate> : IMessageHandler<TValidate>
        where TValidate : Validate
    {
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IServiceProvider _serviceProvider;
        private readonly MessagingOptions _options;

        public ValidationMessageHandler(
            IMessageDispatcher messageDispatcher,
            IServiceProvider serviceProvider,
            IOptions<MessagingOptions> optionsAccessor)
        {
            _messageDispatcher = messageDispatcher;
            _serviceProvider = serviceProvider;
            _options = optionsAccessor.Value ?? new MessagingOptions();
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<TValidate> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            var handlerProvider = _messageDispatcher.MessageHandlerProvider;

            // We mimic the behavior of the message dispatcher to get the validation of
            // the message handler that the message would be dispatched to.
            // We assume that the message will not get published.
            var currType = dispatchData.Message.MessageType;

            do
            {
                var handlerRegistrations = handlerProvider.GetHandlerRegistrations(currType);

                if (handlerRegistrations.Any())
                {
                    foreach (var handlerRegistration in handlerRegistrations)
                    {
                        if (handlerRegistration.IsPublishOnly())
                        {
                            continue;
                        }

                        if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                        {
                            continue;
                        }

                        var validationDispatchData = DispatchDataDictionary.Create(
                            dispatchData.Message.MessageType,
                            dispatchData.Message.Message,
                            dispatchData);

                        var invoker = ValidationInvoker.CreateInvoker(
                            validationDispatchData.MessageType, _options.MessageProcessors, _serviceProvider);

                        return invoker.InvokeValidationAsync(
                            validationDispatchData, publish, localDispatch, handlerRegistration, cancellation);

                        // The message dispatcher has another constraint on the handler.
                        // It skips the handler if it returns a
                        // DispatchFailureDispatchResult. That cannot be checked here
                        // as we are not calling the handler actually. 
                    }
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            return new ValueTask<IDispatchResult>(
                new DispatchFailureDispatchResult(dispatchData.MessageType));
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        ValueTask<IDispatchResult> IMessageHandler.HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            return HandleAsync(dispatchData.As<TValidate>(), publish, localDispatch, cancellation);
        }

        Type IMessageHandler.MessageType => typeof(Validate);
#endif       
    }

    public static class ValidationMessageHandler
    {
        private static void Configure(IMessageHandlerRegistry messageHandlerRegistry, IServiceProvider serviceProvider)
        {
            messageHandlerRegistry.Register(new ValidationMessageHandlerRegistration());
        }

        public static void Register(IMessagingBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            // Protect us from registering the validation-handler multiple times.
            if (builder.Services.Any(p => p.ServiceType == typeof(ValidationMessageHandlerMarker)))
                return;

            builder.Services.AddSingleton<ValidationMessageHandlerMarker>(_ => null);
            builder.ConfigureMessageHandlers(Configure);
        }

        private sealed class ValidationMessageHandlerMarker { }

        private sealed class ValidationMessageHandlerRegistration : IMessageHandlerRegistrationFactory
        {
            public bool TryCreateMessageHandlerRegistration(Type messageType, out IMessageHandlerRegistration handlerRegistration)
            {
                if (!IsValidateMessageType(messageType))
                {
                    handlerRegistration = null;
                    return false;
                }

                // Build the result handler-registration.
                // We do not need to cache this, as the MessageHandlerProvider also caches all handler-registrations.
                var underlyingType = messageType.GetGenericArguments()[0];
                var validationMessageHandlerType = typeof(ValidationMessageHandler<>).MakeGenericType(messageType);
                IMessageHandler messageHandlerFactory(IServiceProvider serviceProvider)
                {
                    return (IMessageHandler)ActivatorUtilities.CreateInstance(serviceProvider, validationMessageHandlerType);
                }

                handlerRegistration = new MessageHandlerRegistration(messageType, messageHandlerFactory);
                return true;
            }

            private bool IsValidateMessageType(Type messageType)
            {
                return messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(Validate<>);
            }
        }
    }
}
