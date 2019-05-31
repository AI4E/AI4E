using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using AI4E.DispatchResults;
using AI4E.Utils;
using BookStore.Alerts;
using Microsoft.AspNetCore.Components;

namespace BookStore.App
{
    public abstract class ComponentBase<TModel> : ComponentBase, IDisposable
    {
        private readonly AsyncLocal<IAlertMessages> _alertMessages;

        #region C'tor

        protected ComponentBase()
        {
            _alertMessages = new AsyncLocal<IAlertMessages>();
        }

        #endregion

        [Inject] private IAlertMessageManager AlertMessageManager { get; set; }
        [Inject] private IDateTimeProvider DateTimeProvider { get; set; }

        protected override void OnInit()
        {
            LoadModel();
        }

        protected internal IAlertMessages AlertMessages
            => _alertMessages.Value ?? AlertMessageManager;

        #region Load

        private Task _loadModelOperation;
        private CancellationTokenSource _loadModelCancellation;
        private IDisposable _previousLoadAlertsDisposer;
        private IAlertMessageManagerScope _loadModelAlerts;
        private readonly object _loadModelMutex = new object();

        protected internal TModel Model { get; private set; }
        protected internal bool IsLoading => Volatile.Read(ref _loadModelOperation) != null;

        protected void LoadModel()
        {
            lock (_loadModelMutex)
            {
                if (_previousLoadAlertsDisposer != null)
                {
                    _previousLoadAlertsDisposer = new CombinedDisposable(
                        _previousLoadAlertsDisposer,
                        _loadModelAlerts);
                }
                else
                {
                    _previousLoadAlertsDisposer = _loadModelAlerts;
                }

                _loadModelAlerts = AlertMessageManager.CreateScope();

                if (_loadModelOperation != null)
                {
                    using (_loadModelCancellation)
                    {
                        _loadModelCancellation.Cancel();
                    }

                    _loadModelOperation.HandleExceptions();
                }

                _loadModelCancellation = new CancellationTokenSource();
                _loadModelOperation = InternalLoadModelAsync(
                    _loadModelAlerts,
                    _loadModelCancellation);
            }
        }

        private async Task InternalLoadModelAsync(
            IAlertMessageManagerScope loadModelAlerts,
            CancellationTokenSource loadModelCancellation)
        {
            // Yield back to the caller to leave the mutex as fast as possible.
            await Task.Yield();

            var cancellation = loadModelCancellation.Token;

            try
            {
                TModel model;

                // Set the ambient alert message handler
                _alertMessages.Value = loadModelAlerts;

                try
                {
                    model = await LoadModelAsync(cancellation);
                }
                finally
                {
                    // Reset the ambient alert message handler
                    _alertMessages.Value = null;
                }

                lock (_loadModelMutex)
                {
                    if (_loadModelCancellation != loadModelCancellation)
                        return;

                    // Clear messages from last model load
                    _previousLoadAlertsDisposer?.Dispose();
                    _previousLoadAlertsDisposer = null;

                    if (model != null)
                    {
                        Model = model;
                    }

                    _loadModelOperation = null;
                    _loadModelCancellation.Dispose();
                }

                if (model != null)
                {
                    try
                    {
                        OnModelLoaded();
                        OnModelLoadedAsync().HandleExceptions();
                    }
                    finally
                    {
                        StateHasChanged();
                    }
                }
            }
            catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }

        protected virtual ValueTask<TModel> LoadModelAsync(CancellationToken cancellation)
        {
            TModel model;
            try
            {
                model = Activator.CreateInstance<TModel>();
            }
            catch (MissingMethodException exc)
            {
                throw new InvalidOperationException("Cannot create a model of type {typeof(TModel)}. The type does not have a public default constructor.", exc);
            }

            return new ValueTask<TModel>(model);
        }

        protected virtual async ValueTask<TModel> EvaluateLoadResultAsync(IDispatchResult dispatchResult)
        {
            if (IsSuccess(dispatchResult, out var model))
            {
                await OnLoadSuccessAsync(model, dispatchResult);
                return model;
            }

            await EvaluateFailureResultAsync(dispatchResult);

            return default;
        }

        protected virtual ValueTask OnLoadSuccessAsync(TModel model, IDispatchResult dispatchResult)
        {
            return default;
        }

        protected virtual bool IsSuccess(IDispatchResult dispatchResult, out TModel model)
        {
            return dispatchResult.IsSuccessWithResult(out model);
        }

        protected virtual void OnModelLoaded() { }

        protected virtual Task OnModelLoadedAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Failure result evaluation

        private ValueTask EvaluateFailureResultAsync(IDispatchResult dispatchResult)
        {
            if (dispatchResult.IsValidationFailed())
            {
                return OnValidationFailureAsync(dispatchResult);
            }

            if (dispatchResult.IsConcurrencyIssue())
            {
                return OnConcurrencyIssueAsync(dispatchResult);
            }

            if (dispatchResult.IsNotFound())
            {
                return OnNotFoundAsync(dispatchResult);
            }

            if (dispatchResult.IsNotAuthenticated())
            {
                return OnNotAuthenticatedAsync(dispatchResult);
            }

            if (dispatchResult.IsNotAuthorized())
            {
                return OnNotAuthorizedAsync(dispatchResult);
            }

            return OnFailureAsync(dispatchResult);
        }

        protected static string GetValidationMessage(IDispatchResult dispatchResult)
        {
            if (dispatchResult.IsAggregateResult(out var aggregateDispatchResult))
            {
                var dispatchResults = aggregateDispatchResult.Flatten().DispatchResults;

                if (dispatchResults.Count() == 1)
                {
                    dispatchResult = dispatchResults.First();
                }
            }

            if (dispatchResult is ValidationFailureDispatchResult && !string.IsNullOrWhiteSpace(dispatchResult.Message))
            {
                return dispatchResult.Message;
            }

            return "Validation failed.";
        }

        protected virtual ValueTask OnValidationFailureAsync(IDispatchResult dispatchResult)
        {
            var validationResults = (dispatchResult as ValidationFailureDispatchResult)
                ?.ValidationResults
                ?? Enumerable.Empty<ValidationResult>();
            var validationMessages = validationResults.Where(p => string.IsNullOrWhiteSpace(p.Member)).Select(p => p.Message);

            if (!validationMessages.Any())
            {
                validationMessages = Enumerable.Repeat(GetValidationMessage(dispatchResult), 1);
            }

            foreach (var validationMessage in validationMessages)
            {
                var alert = new AlertMessage(AlertType.Danger, validationMessage);
                AlertMessages.PlaceAlert(alert);
            }

            return default;
        }

        protected virtual ValueTask OnConcurrencyIssueAsync(IDispatchResult dispatchResult)
        {
            var alert = new AlertMessage(AlertType.Danger, "A concurrency issue occured.");
            AlertMessages.PlaceAlert(alert);
            return default;
        }

        protected virtual ValueTask OnNotFoundAsync(IDispatchResult dispatchResult)
        {
            var alert = new AlertMessage(AlertType.Info, "Not found.");
            AlertMessages.PlaceAlert(alert);
            return default;
        }

        protected virtual ValueTask OnNotAuthenticatedAsync(IDispatchResult dispatchResult)
        {
            var alert = new AlertMessage(AlertType.Info, "Not authenticated.");
            AlertMessages.PlaceAlert(alert);
            return default;
        }

        protected virtual ValueTask OnNotAuthorizedAsync(IDispatchResult dispatchResult)
        {
            var alert = new AlertMessage(AlertType.Info, "Not authorized.");
            AlertMessages.PlaceAlert(alert);
            return default;
        }

        protected virtual ValueTask OnFailureAsync(IDispatchResult dispatchResult)
        {
            var alertMessage = "An unexpected error occured.";
            var alert = new AlertMessage(AlertType.Danger, alertMessage);

            AlertMessages.PlaceAlert(alert);
            return default;
        }

        #endregion

        #region Store

        protected virtual ValueTask EvaluateStoreResultAsync(IDispatchResult dispatchResult)
        {
            if (dispatchResult.IsSuccess)
            {
                return OnStoreSuccessAsync(Model, dispatchResult);
            }

            return EvaluateFailureResultAsync(dispatchResult);
        }

        protected virtual ValueTask OnStoreSuccessAsync(TModel model, IDispatchResult dispatchResult)
        {
            var alert = new AlertMessage(
                AlertType.Success,
                "Successfully performed operation.",
                expiration: DateTimeProvider.GetCurrentTime() + TimeSpan.FromSeconds(10));
            AlertMessages.PlaceAlert(alert);
            return default;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            lock (_loadModelMutex)
            {
                if (_loadModelOperation != null)
                {
                    try
                    {
                        _loadModelCancellation.Cancel();
                    }
                    catch (ObjectDisposedException) { }
                }
            }

            Dispose(true);
        }

        #endregion
    }
}
