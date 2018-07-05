using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleSourceDeleteModel
    {
        public Guid Id { get; set; }
        public string ConcurrencyToken { get; set; }
        public string Name { get; set; }
    }
}
