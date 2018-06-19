using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AI4E.Storage.Sample
{
    public sealed class TestEntity
    {
        private string _value;
        [JsonProperty(propertyName: nameof(ChildIds))]
        private readonly ISet<string> _childIds = new HashSet<string>();

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

        [JsonIgnore]
        public IEnumerable<string> ChildIds => _childIds;

        public void AddChild(ChildEntity child)
        {
            _childIds.Add(child.Id);
        }

        public void RemoveChild(ChildEntity child)
        {
            _childIds.Remove(child.Id);
        }
    }

    public sealed class ChildEntity
    {
        public ChildEntity(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}
