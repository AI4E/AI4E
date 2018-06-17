using System;

namespace AI4E.Storage.Sample
{
    public sealed class TestEntityValueChanged
    {
        public TestEntityValueChanged(string id, string value)
        {
            Id = id;
            Value = value;
        }

        public string Id { get; }

        public string Value { get; }
    }
}
