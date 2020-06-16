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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AspNet Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using AI4E.AspNetCore.Components.Factory.Reflection;
using Microsoft.AspNetCore.Components;

namespace AI4E.AspNetCore.Components.Factory
{
    public class ComponentActivator : IComponentActivator
    {
        private static readonly BindingFlags _injectablePropertyBindingFlags
            = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly ConcurrentDictionary<Type, Action<IServiceProvider, IComponent>> _cachedInitializers
            = new ConcurrentDictionary<Type, Action<IServiceProvider, IComponent>>();

        private readonly IServiceProvider _serviceProvider;

        public ComponentActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public virtual IComponent ActivateComponent(Type componentType)
        {
            // Default component instantiation
            // https://github.com/aspnet/AspNetCore/blob/master/src/Components/Components/src/ComponentFactory.cs#L28

            var instance = Activator.CreateInstance(componentType);
            if (!(instance is IComponent component))
            {
                throw new ArgumentException($"The type {componentType.FullName} does not implement {nameof(IComponent)}.", nameof(componentType));
            }

            PerformPropertyInjection(_serviceProvider, component);
            return component;
        }

        private void PerformPropertyInjection(IServiceProvider serviceProvider, IComponent instance)
        {
            // This is thread-safe because _cachedInitializers is a ConcurrentDictionary.
            // We might generate the initializer more than once for a given type, but would
            // still produce the correct result.
            var instanceType = instance.GetType();
            if (!_cachedInitializers.TryGetValue(instanceType, out var initializer))
            {
                initializer = CreateInitializer(instanceType);
                _cachedInitializers.TryAdd(instanceType, initializer);
            }

            initializer(serviceProvider, instance);
        }

        private Action<IServiceProvider, IComponent> CreateInitializer(Type type)
        {
            // Do all the reflection up front
            var injectableProperties =
                MemberAssignment.GetPropertiesIncludingInherited(type, _injectablePropertyBindingFlags)
                .Where(p => p.IsDefined(typeof(InjectAttribute)));

            var injectables = injectableProperties.Select(property =>
            (
                propertyName: property.Name,
                propertyType: property.PropertyType,
                setter: MemberAssignment.CreatePropertySetter(type, property, cascading: false)
            )).ToArray();

            return Initialize;

            // Return an action whose closure can write all the injected properties
            // without any further reflection calls (just typecasts)
            void Initialize(IServiceProvider serviceProvider, IComponent component)
            {
                foreach (var (propertyName, propertyType, setter) in injectables)
                {
                    var serviceInstance = serviceProvider.GetService(propertyType);
                    if (serviceInstance == null)
                    {
                        throw new InvalidOperationException($"Cannot provide a value for property " +
                            $"'{propertyName}' on type '{type.FullName}'. There is no " +
                            $"registered service of type '{propertyType}'.");
                    }

                    setter.SetValue(component, serviceInstance);
                }
            }
        }
    }
}
