using System;
using System.Collections.Generic;
using ComponentBaseTest.Models;

namespace ComponentBaseTest.Services
{
    public interface IResourceRepository
    {
        IEnumerable<Resource> GetResources();
        Resource? GetResourceById(Guid id);

        bool TryAddResource(Resource resource);
        bool TryRemoveResource(Resource resource);
        bool TryUpdateResource(Resource resource);
    }
}
