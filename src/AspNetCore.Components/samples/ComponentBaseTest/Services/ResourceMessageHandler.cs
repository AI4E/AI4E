using System;
using System.Collections.Generic;
using AI4E.Messaging;
using AI4E.Messaging.Validation;
using AI4E.Utils;
using ComponentBaseTest.Messages;
using ComponentBaseTest.Models;

namespace ComponentBaseTest.Services
{
    public sealed class ResourceMessageHandler : MessageHandler
    {
        private readonly IResourceRepository _resources;

        public ResourceMessageHandler(IResourceRepository resources)
        {
            _resources = resources;
        }

        [Validate]
        public DispatchResult Handle(Update<Resource> message)
        {
            var model = message.Data;

            if (model.ConcurrencyToken == Guid.Empty)
            {
                if (!_resources.TryAddResource(model))
                {
                    return ConcurrencyIssue();
                }

                return Success();
            }

            if (!_resources.TryUpdateResource(model))
            {
                // -- TODO --
                // The resource either does not exist, or a concurrency conflict occurs.
                // We cannot distinguish these cases thread-safely here without changing the resource respository.

                if (_resources.GetResourceById(model.Id) != null)
                {
                    return ConcurrencyIssue();
                }

                return EntityNotFound<Resource>(model.Id);

            }

            return Success();
        }

        public void Validate(
            Update<Resource> message,
            ValidationResultsBuilder validationResults,
            IDateTimeProvider dateTimeProvider)
        {
            var model = message.Data;

            if (model.Amount < 0)
            {
                validationResults.AddValidationResult(
                    nameof(model.Amount),
                    "A resource's amount must not be negative.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                validationResults.AddValidationResult(
                    nameof(model.Name),
                    "A name must be specified for the resource.");
            }

            if (model.DateOfCreation != null && model.DateOfCreation > dateTimeProvider.GetCurrentTime())
            {
                validationResults.AddValidationResult(
                    nameof(model.DateOfCreation),
                    "If specified, a resource's date of creation must not be a date in the future.");
            }
        }

        public DispatchResult Handle(Delete<Resource> message)
        {
            var model = message.Data;
            var success = _resources.TryRemoveResource(model);

            if (!success)
            {
                return ConcurrencyIssue();
            }

            return Success();
        }

        public Resource? Handle(ByIdQuery<Resource> query)
        {
            return _resources.GetResourceById(query.Id);
        }

        public IEnumerable<Resource> Handle(Query<IEnumerable<Resource>> query)
        {
            return _resources.GetResources();
        }
    }
}
