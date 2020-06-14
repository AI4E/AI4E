using System;

namespace Routing.ModularRouterSample
{
    public sealed class FormsModel
    {
        public Guid Id { get; set; }
        public string String { get; set; }
        public int Int { get; set; }
    }

    public sealed class PluginFormsModel
    {
        public Guid Id { get; set; }
        public bool Bool { get; set; }
    }
}
