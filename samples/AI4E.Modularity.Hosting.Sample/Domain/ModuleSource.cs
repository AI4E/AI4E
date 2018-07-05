using System;
using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleSource : AggregateRoot
    {
        private ModuleSourceName _name;
        private ModuleSourceLocation _location;

        public ModuleSource(Guid id, ModuleSourceName name, ModuleSourceLocation location) : base(id)
        {
            if (name == default)
                throw new ArgumentDefaultException(nameof(name));

            if (location == default)
                throw new ArgumentDefaultException(nameof(location));

            _name = name;
            _location = location;

            Notify(new ModuleSourceAdded(id, location));
        }

        public ModuleSourceName Name
        {
            get => _name;
            set
            {
                if (value == _name)
                    return;

                if (value == default)
                    throw new ArgumentDefaultException(nameof(value));

                _name = value;
            }
        }

        public ModuleSourceLocation Location
        {
            get => _location;
            set
            {
                if (value == _location)
                    return;

                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _location = value;

                Notify(new ModuleSourceLocationChanged(Id, value));
            }
        }

        protected override void DoDispose()
        {
            Notify(new ModuleSourceRemoved(Id));
        }
    }
}
