using System;
using System.Collections.Generic;
using System.Text;

namespace AI4E.Storage.Sample
{
    public sealed class DependentEntity
    {
        public DependentEntity(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public string DependencyId { get; set; }
    }
}
