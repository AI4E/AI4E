using System;

namespace AI4E.Storage.Sample
{
    public sealed class TestEntityModel
    {
        public string Id { get; set; }
        public string ConcurrencyToken { get; set; }
        public string Value { get; set; }
    }
}
