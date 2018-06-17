using System;

namespace AI4E.Storage.Sample
{
    public sealed class TestEntity
    {
        private string _value;

        public TestEntity(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (value == _value)
                    return;

                _value = value;

                var evt = new TestEntityValueChanged(Id, value);

                // TODO: Publish event.
            }
        }
    }
}
