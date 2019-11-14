using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ComponentBaseTest.Models;

namespace ComponentBaseTest.Services
{
    public sealed class ResourceRepository : IResourceRepository
    {
        private readonly ConcurrentDictionary<Guid, Resource> _resources = new ConcurrentDictionary<Guid, Resource>();

        public IEnumerable<Resource> GetResources()
        {
            return DeepCloneResources(_resources.Values).ToImmutableList();
        }

        public Resource? GetResourceById(Guid id)
        {
            if (!_resources.TryGetValue(id, out var resource))
            {
                resource = null;
            }

            return DeepCloneResource(resource);
        }

        public bool TryAddResource(Resource resource)
        {
            var copy = DeepCloneResource(resource);
            copy.ConcurrencyToken = CreateConcurrencyToken();

            return _resources.TryAdd(resource.Id, copy);
        }

        public bool TryRemoveResource(Resource resource)
        {
            Resource? comparand;

            do
            {
                if (!_resources.TryGetValue(resource.Id, out comparand))
                {
                    // We cannot remove the resource, as it is not existing.
                    // As our result does not indicate removal success but concurrency check, this is indeed a success.

                    return true;
                }

                if (comparand.ConcurrencyToken != resource.ConcurrencyToken)
                {
                    return false;
                }
            }
            while (!_resources.TryRemove(resource.Id, comparand));

            return true;
        }

        public bool TryUpdateResource(Resource resource)
        {
            Resource? comparand;
            Resource? copy = null;

            do
            {
                if (!_resources.TryGetValue(resource.Id, out comparand))
                {
                    // We cannot remove the resource, as it is not existing.
                    // As our result does not indicate removal success but concurrency check, this is indeed a success.

                    return false;
                }

                if (comparand.ConcurrencyToken != resource.ConcurrencyToken)
                {
                    return false;
                }

                if (copy is null)
                {
                    copy = DeepCloneResource(resource);
                    copy.ConcurrencyToken = CreateConcurrencyToken();
                }

            }
            while (!_resources.TryUpdate(resource.Id, copy, comparand));

            return true;
        }

        [return: NotNullIfNotNull("resource")]
        private Resource? DeepCloneResource(Resource? resource)
        {
            if (resource is null)
                return null;

            return new Resource
            {
                Id = resource.Id,
                ConcurrencyToken = resource.ConcurrencyToken,
                Name = resource.Name,
                Amount = resource.Amount,
                DateOfCreation = resource.DateOfCreation
            };
        }

        private IEnumerable<Resource> DeepCloneResources(IEnumerable<Resource> resources)
        {
            foreach (var resource in resources)
            {
                yield return DeepCloneResource(resource);
            }
        }

        private Guid CreateConcurrencyToken()
        {
            Guid result;

            while ((result = Guid.NewGuid()) == Guid.Empty) ;

            return result;
        }
    }
}
