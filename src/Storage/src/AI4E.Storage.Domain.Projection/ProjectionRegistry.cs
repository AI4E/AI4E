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

// TODO: This is a 100% copy of MessageHandlerRegistry.
// Can we provide a generic implementation that works for both cases?

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Utils;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a registry where projections can be registered.
    /// </summary>
    public sealed class ProjectionRegistry : IProjectionRegistry
    {
        private readonly Dictionary<Type, OrderedSet<IProjectionRegistration>> _projectionRegistrations;

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionRegistry"/> type.
        /// </summary>
        public ProjectionRegistry()
        {
            _projectionRegistrations = new Dictionary<Type, OrderedSet<IProjectionRegistration>>();
        }

        /// <inheritdoc />
        public bool Register(IProjectionRegistration projectionRegistration)
        {
            if (projectionRegistration == null)
                throw new ArgumentNullException(nameof(projectionRegistration));

            var handlerCollection = _projectionRegistrations.GetOrAdd(projectionRegistration.SourceType, _ => new OrderedSet<IProjectionRegistration>());
            var result = true;

            if (handlerCollection.Remove(projectionRegistration))
            {
                result = false; // TODO: Does this conform with spec?
            }

            handlerCollection.Add(projectionRegistration);

            return result;
        }

        /// <inheritdoc />
        public bool Unregister(IProjectionRegistration projectionRegistration)
        {
            if (projectionRegistration == null)
                throw new ArgumentNullException(nameof(projectionRegistration));

            if (!_projectionRegistrations.TryGetValue(projectionRegistration.SourceType, out var handlerCollection))
            {
                return false;
            }

            if (!handlerCollection.Remove(projectionRegistration))
            {
                return false;
            }

            if (!handlerCollection.Any())
            {
                _projectionRegistrations.Remove(projectionRegistration.SourceType);
            }

            return true;
        }

        /// <inheritdoc />
        public IProjectionProvider ToProvider()
        {
            return new ProjectionProvider(_projectionRegistrations);
        }

        private sealed class ProjectionProvider : IProjectionProvider
        {
            private readonly ImmutableDictionary<Type, ImmutableList<IProjectionRegistration>> _projectionRegistrations;
            private readonly ImmutableList<IProjectionRegistration> _combinedRegistrations;

            public ProjectionProvider(Dictionary<Type, OrderedSet<IProjectionRegistration>> projectionRegistrations)
            {
                _projectionRegistrations = projectionRegistrations.ToImmutableDictionary(
                    keySelector: kvp => kvp.Key,
                    elementSelector: kvp => kvp.Value.Reverse().ToImmutableList()); // TODO: Reverse is not very permanent

                _combinedRegistrations = BuildCombinedCollection().ToImmutableList();
            }

            private IEnumerable<IProjectionRegistration> BuildCombinedCollection()
            {
                foreach (var type in _projectionRegistrations.Keys)
                {
                    foreach (var handler in GetProjectionRegistrations(type))
                    {
                        yield return handler;
                    }
                }
            }

            public IReadOnlyList<IProjectionRegistration> GetProjectionRegistrations(Type sourceType)
            {
                if (!_projectionRegistrations.TryGetValue(sourceType, out var result))
                {
                    result = ImmutableList<IProjectionRegistration>.Empty;
                }

                return result;
            }

            public IReadOnlyList<IProjectionRegistration> GetProjectionRegistrations()
            {
                return _combinedRegistrations;
            }
        }
    }
}
