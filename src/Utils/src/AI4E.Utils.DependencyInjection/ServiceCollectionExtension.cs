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

// Based on: https://github.com/khellang/Scrutor/blob/master/src/Scrutor/ServiceCollectionExtensions.Decoration.cs

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AI4E.Utils.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AI4EUtilsServiceCollectionExtension
    {
        [return: MaybeNull]
        public static T GetService<T>(this IServiceCollection services)
        {
            var serviceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(T));

            var instance = serviceDescriptor?.ImplementationInstance;

            if (instance is null)
            {
                return default!;
            }

            return (T)instance;
        }

        /// <summary>
        /// Decorates all registered services of type <typeparamref name="TService"/>
        /// using the specified type <typeparamref name="TDecorator"/>.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <exception cref="MissingTypeRegistrationException">
        /// If no service of the type <typeparamref name="TService"/> has been registered.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// If the <paramref name="services"/> argument is <c>null</c>.
        /// </exception>
        public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
            where TDecorator : TService
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return services.DecorateDescriptors(typeof(TService), x => x.Decorate(typeof(TDecorator)));
        }

        /// <summary>
        /// Decorates all registered services of type <typeparamref name="TService"/>
        /// using the specified type <typeparamref name="TDecorator"/>.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <exception cref="ArgumentNullException">
        /// If the <paramref name="services"/> argument is <c>null</c>.
        /// </exception>
        public static bool TryDecorate<TService, TDecorator>(this IServiceCollection services)
            where TDecorator : TService
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return services.TryDecorateDescriptors(typeof(TService), x => x.Decorate(typeof(TDecorator)));
        }

        /// <summary>
        /// Decorates all registered services of the specified <paramref name="serviceType"/>
        /// using the specified <paramref name="decoratorType"/>.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <param name="serviceType">The type of services to decorate.</param>
        /// <param name="decoratorType">The type to decorate existing services with.</param>
        /// <exception cref="MissingTypeRegistrationException">
        /// If no service of the specified <paramref name="serviceType"/> has been registered.
        /// </exception>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
        /// <paramref name="serviceType"/> or <paramref name="decoratorType"/> arguments are <c>null</c>.</exception>
        public static IServiceCollection Decorate(
            this IServiceCollection services, Type serviceType, Type decoratorType)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decoratorType == null)
                throw new ArgumentNullException(nameof(decoratorType));

            if (serviceType.IsGenericTypeDefinition && decoratorType.IsGenericTypeDefinition)
            {
                return services.DecorateOpenGeneric(serviceType, decoratorType);
            }

            return services.DecorateDescriptors(serviceType, x => x.Decorate(decoratorType));
        }

        /// <summary>
        /// Decorates all registered services of the specified <paramref name="serviceType"/>
        /// using the specified <paramref name="decoratorType"/>.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <param name="serviceType">The type of services to decorate.</param>
        /// <param name="decoratorType">The type to decorate existing services with.</param>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
        /// <paramref name="serviceType"/> or <paramref name="decoratorType"/> arguments are <c>null</c>.</exception>
        public static bool TryDecorate(this IServiceCollection services, Type serviceType, Type decoratorType)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decoratorType == null)
                throw new ArgumentNullException(nameof(decoratorType));

            if (serviceType.IsGenericTypeDefinition && decoratorType.IsGenericTypeDefinition)
            {
                return services.TryDecorateOpenGeneric(serviceType, decoratorType);
            }

            return services.TryDecorateDescriptors(serviceType, x => x.Decorate(decoratorType));
        }

        /// <summary>
        /// Decorates all registered services of type <typeparamref name="TService"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <typeparam name="TService">The type of services to decorate.</typeparam>
        /// <param name="services">The services to add to.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="MissingTypeRegistrationException">
        /// If no service of <typeparamref name="TService"/> has been registered.
        /// </exception>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
        /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static IServiceCollection Decorate<TService>(
            this IServiceCollection services, Func<TService, IServiceProvider, TService> decorator)
                where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        public static IServiceCollection Decorate<TService>(
            this IServiceCollection services, Func<Func<TService>, IServiceProvider, TService> decorator)
                where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of type <typeparamref name="TService"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <typeparam name="TService">The type of services to decorate.</typeparam>
        /// <param name="services">The services to add to.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
        /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static bool TryDecorate<TService>(
            this IServiceCollection services, Func<TService, IServiceProvider, TService> decorator)
                where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        public static bool TryDecorate<TService>(
            this IServiceCollection services, Func<Func<TService>, IServiceProvider, TService> decorator)
                where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of type <typeparamref name="TService"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <typeparam name="TService">The type of services to decorate.</typeparam>
        /// <param name="services">The services to add to.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="MissingTypeRegistrationException">
        /// If no service of <typeparamref name="TService"/> has been registered.
        /// </exception>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
        /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static IServiceCollection Decorate<TService>(
            this IServiceCollection services, Func<TService, TService> decorator)
                where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        public static IServiceCollection Decorate<TService>(
           this IServiceCollection services, Func<Func<TService>, TService> decorator)
               where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of type <typeparamref name="TService"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <typeparam name="TService">The type of services to decorate.</typeparam>
        /// <param name="services">The services to add to.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
        /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static bool TryDecorate<TService>(this IServiceCollection services, Func<TService, TService> decorator)
                where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        public static bool TryDecorate<TService>(this IServiceCollection services, Func<Func<TService>, TService> decorator)
               where TService : notnull
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(typeof(TService), x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of the specified <paramref name="serviceType"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <param name="serviceType">The type of services to decorate.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="MissingTypeRegistrationException">
        /// If no service of the specified <paramref name="serviceType"/> has been registered.
        /// </exception>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
        /// <paramref name="serviceType"/> or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static IServiceCollection Decorate(
            this IServiceCollection services, Type serviceType, Func<object, IServiceProvider, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        public static IServiceCollection Decorate(
           this IServiceCollection services, Type serviceType, Func<Func<object>, IServiceProvider, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of the specified <paramref name="serviceType"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <param name="serviceType">The type of services to decorate.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
        /// <paramref name="serviceType"/> or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static bool TryDecorate(
            this IServiceCollection services, Type serviceType, Func<object, IServiceProvider, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        public static bool TryDecorate(
            this IServiceCollection services, Type serviceType, Func<Func<object>, IServiceProvider, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of the specified <paramref name="serviceType"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <param name="serviceType">The type of services to decorate.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="MissingTypeRegistrationException">
        /// If no service of the specified <paramref name="serviceType"/> has been registered.</exception>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
        /// <paramref name="serviceType"/> or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static IServiceCollection Decorate(
            this IServiceCollection services, Type serviceType, Func<object, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        public static IServiceCollection Decorate(
           this IServiceCollection services, Type serviceType, Func<Func<object>, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.DecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        /// <summary>
        /// Decorates all registered services of the specified <paramref name="serviceType"/>
        /// using the <paramref name="decorator"/> function.
        /// </summary>
        /// <param name="services">The services to add to.</param>
        /// <param name="serviceType">The type of services to decorate.</param>
        /// <param name="decorator">The decorator function.</param>
        /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
        /// <paramref name="serviceType"/> or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
        public static bool TryDecorate(
            this IServiceCollection services, Type serviceType, Func<object, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        public static bool TryDecorate(
           this IServiceCollection services, Type serviceType, Func<Func<object>, object> decorator)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            return services.TryDecorateDescriptors(serviceType, x => x.Decorate(decorator));
        }

        private static IServiceCollection DecorateOpenGeneric(
            this IServiceCollection services, Type serviceType, Type decoratorType)
        {
            if (services.TryDecorateOpenGeneric(serviceType, decoratorType))
            {
                return services;
            }

            throw new MissingTypeRegistrationException(serviceType);
        }

        private static bool TryDecorateOpenGeneric(
            this IServiceCollection services, Type serviceType, Type decoratorType)
        {
            bool TryDecorate(Type[] typeArguments)
            {
                var closedServiceType = serviceType.MakeGenericType(typeArguments);
                var closedDecoratorType = decoratorType.MakeGenericType(typeArguments);

                return services.TryDecorateDescriptors(closedServiceType, x => x.Decorate(closedDecoratorType));
            }

            var arguments = services
                .Where(descriptor => descriptor.ServiceType.IsAssignableTo(serviceType))
                .Select(descriptor => descriptor.ServiceType.GenericTypeArguments)
                .ToArray();

            if (arguments.Length == 0)
            {
                return false;
            }

            return arguments.Aggregate(true, (result, args) => result && TryDecorate(args));
        }

        private static IServiceCollection DecorateDescriptors(
            this IServiceCollection services, Type serviceType, Func<ServiceDescriptor, ServiceDescriptor> decorator)
        {
            if (services.TryDecorateDescriptors(serviceType, decorator))
            {
                return services;
            }

            throw new MissingTypeRegistrationException(serviceType);
        }

        private static bool TryDecorateDescriptors(
            this IServiceCollection services, Type serviceType, Func<ServiceDescriptor, ServiceDescriptor> decorator)
        {
            if (!services.TryGetDescriptors(serviceType, out var descriptors))
            {
                return false;
            }

            foreach (var descriptor in descriptors)
            {
                var index = services.IndexOf(descriptor);

                // To avoid reordering descriptors, in case a specific order is expected.
                services.Insert(index, decorator(descriptor));

                services.Remove(descriptor);
            }

            return true;
        }

        private static bool TryGetDescriptors(
            this IServiceCollection services, Type serviceType, out ICollection<ServiceDescriptor> descriptors)
        {
            return (descriptors = services.Where(service => service.ServiceType == serviceType).ToArray()).Any();
        }

        private static ServiceDescriptor Decorate<TService>(
            this ServiceDescriptor descriptor, Func<TService, IServiceProvider, TService> decorator)
            where TService : notnull
        {
            return descriptor.WithFactory(provider => decorator((TService)provider.GetInstance(descriptor), provider));
        }

        private static ServiceDescriptor Decorate<TService>(
            this ServiceDescriptor descriptor, Func<Func<TService>, IServiceProvider, TService> decorator)
            where TService : notnull
        {
            return descriptor.WithFactory(provider => decorator(() => (TService)provider.GetInstance(descriptor), provider));
        }

        private static ServiceDescriptor Decorate<TService>(
            this ServiceDescriptor descriptor, Func<TService, TService> decorator)
                where TService : notnull
        {
            return descriptor.WithFactory(provider => decorator((TService)provider.GetInstance(descriptor)));
        }

        private static ServiceDescriptor Decorate<TService>(
           this ServiceDescriptor descriptor, Func<Func<TService>, TService> decorator)
               where TService : notnull
        {
            return descriptor.WithFactory(provider => decorator(() => (TService)provider.GetInstance(descriptor)));
        }

        private static ServiceDescriptor Decorate(
            this ServiceDescriptor descriptor, Type decoratorType)
        {
            return descriptor.WithFactory(
                provider => provider.CreateInstance(decoratorType, provider.GetInstance(descriptor)));
        }

        private static ServiceDescriptor WithFactory(
            this ServiceDescriptor descriptor, Func<IServiceProvider, object> factory)
        {
            return ServiceDescriptor.Describe(descriptor.ServiceType, factory, descriptor.Lifetime);
        }

        private static object GetInstance(this IServiceProvider provider, ServiceDescriptor descriptor)
        {
            if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }

            if (descriptor.ImplementationType != null)
            {
                return provider.GetServiceOrCreateInstance(descriptor.ImplementationType);
            }

            return descriptor.ImplementationFactory(provider);
        }

        private static object GetServiceOrCreateInstance(this IServiceProvider provider, Type type)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(provider, type);
        }

        private static object CreateInstance(this IServiceProvider provider, Type type, params object[] arguments)
        {
            return ActivatorUtilities.CreateInstance(provider, type, arguments);
        }
    }
}
