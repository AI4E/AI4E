using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using AI4E.Utils;

namespace AI4E
{
    public readonly struct MessageHandlerConfiguration
    {
        private readonly ImmutableDictionary<Type, object> _data;

        private MessageHandlerConfiguration(ImmutableDictionary<Type, object> data)
        {
            _data = data;
        }

        private bool TryGetConfiguration(Type configurationType, out object configuration)
        {
            if (configurationType == null)
                throw new ArgumentNullException(nameof(configurationType));

            if (!configurationType.IsOrdinaryClass())
                throw new ArgumentException("The specified type must be an ordinary reference type.", nameof(configurationType));

            return _data.TryGetValue(configurationType, out configuration);
        }

        public bool TryGetConfiguration<TConfig>(
            out TConfig configuration)
            where TConfig : class
        {
            configuration = default;
            return TryGetConfiguration(typeof(TConfig), out Unsafe.As<TConfig, object>(ref configuration));
        }

        public TConfig GetConfiguration<TConfig>()
            where TConfig : class, new()
        {
            if (!TryGetConfiguration<TConfig>(out var config))
            {
                config = new TConfig();
            }

            return config;
        }

        public bool IsEnabled<TFeature>(bool defaultValue = false)
             where TFeature : class, IMessageHandlerConfigurationFeature
        {
            if (!TryGetConfiguration<TFeature>(out var config))
            {
                return defaultValue;
            }

            return config.IsEnabled;
        }

        public static MessageHandlerConfiguration FromDescriptor(in MessageHandlerActionDescriptor memberDescriptor)
        {
            var configurationBuilder = new MessageHandlerConfigurationBuilder();

            foreach (var typeConfigAttribute in memberDescriptor.MessageHandlerType.Assembly.GetCustomAttributes<ConfigureMessageHandlerAttribute>(inherit: true))
            {
                typeConfigAttribute.ExecuteConfigureMessageHandler(memberDescriptor, configurationBuilder);
            }

            foreach (var typeConfigAttribute in memberDescriptor.MessageHandlerType.GetCustomAttributes<ConfigureMessageHandlerAttribute>(inherit: true))
            {
                typeConfigAttribute.ExecuteConfigureMessageHandler(memberDescriptor, configurationBuilder);
            }

            foreach (var actionConfigAttribute in memberDescriptor.Member.GetCustomAttributes<ConfigureMessageHandlerAttribute>(inherit: true))
            {
                actionConfigAttribute.ExecuteConfigureMessageHandler(memberDescriptor, configurationBuilder);
            }

            return configurationBuilder.Build();
        }

        private sealed class MessageHandlerConfigurationBuilder : IMessageHandlerConfigurationBuilder
        {
            private readonly ImmutableDictionary<Type, object>.Builder _dataBuilder;

            public MessageHandlerConfigurationBuilder()
            {
                _dataBuilder = ImmutableDictionary.CreateBuilder<Type, object>();
            }

            private IMessageHandlerConfigurationBuilder Configure(
                Type configurationType,
                Func<object, object> configuration)
            {
                if (configurationType == null)
                    throw new ArgumentNullException(nameof(configurationType));

                if (!configurationType.IsOrdinaryClass())
                    throw new ArgumentException("The specified type must be an ordinary reference type.", nameof(configurationType));

                if (!_dataBuilder.TryGetValue(configurationType, out var obj))
                {
                    obj = null;
                }

                if (obj == null)
                {
                    _dataBuilder.Remove(configurationType);
                }
                else
                {
                    if (!configurationType.IsAssignableFrom(obj.GetType()))
                    {
                        throw new InvalidOperationException();
                    }

                    _dataBuilder[configurationType] = obj;
                }

                return this;
            }

            public IMessageHandlerConfigurationBuilder Configure<TConfig>(
                Func<TConfig, TConfig> configuration)
                where TConfig : class
            {
                return Configure(typeof(TConfig), o => configuration(o as TConfig));
            }

            public IMessageHandlerConfigurationBuilder Configure<TConfig>(
                Func<TConfig> configuration)
                where TConfig : class
            {
                return Configure(typeof(TConfig), o => configuration());
            }

            public IMessageHandlerConfigurationBuilder Configure<TConfig>(
                Action<TConfig> configuration)
                where TConfig : class, new()
            {
                object Configuration(object obj)
                {
                    var config = obj as TConfig ?? new TConfig();
                    configuration(config);

                    return config;
                }

                return Configure(typeof(TConfig), Configuration);
            }

            public MessageHandlerConfiguration Build()
            {
                var data = _dataBuilder.ToImmutable();
                return new MessageHandlerConfiguration(data);
            }
        }
    }

    public interface IMessageHandlerConfigurationBuilder
    {
        IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Func<TConfig, TConfig> configuration)
            where TConfig : class;

        IMessageHandlerConfigurationBuilder Configure<TConfig>(
           Func<TConfig> configuration)
           where TConfig : class;

        IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Action<TConfig> configuration)
            where TConfig : class, new();
    }

    public interface IMessageHandlerConfigurationFeature
    {
        bool IsEnabled { get; }
    }
}
