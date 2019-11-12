/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Notifications;
using AI4E.Messaging;
using AI4E.Messaging.Validation;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.AspNetCore.Components
{
    public abstract class ComponentBase<TModel> : ComponentBase, IDisposable
        where TModel : class, new()
    {
        #region Fields

        private readonly AsyncLocal<OperationContext?> _ambientOperationContext;
        private readonly Lazy<ILogger?> _logger;
        private readonly AsyncLock _loadModelMutex = new AsyncLock();
        private ILogger? Logger => _logger.Value;
        private TModel? _model;

#pragma warning disable IDE0069, IDE0044, CA2213
        private OperationContext? _lastSuccessOperationContext;
        private CancellationTokenSource? _pendingOperationCancellationSource;
        private ValueTaskCompletionSource<TModel?> _pendingOperationCompletion = ValueTaskCompletionSource.Create<TModel?>();
#pragma warning restore IDE0069, IDE0044, CA2213

        #endregion

        #region C'tor

        protected ComponentBase()
        {
            _ambientOperationContext = new AsyncLocal<OperationContext?>();
            _logger = new Lazy<ILogger?>(BuildLogger);

            // These will be set by DI. Just to disable warnings here.
            NotificationManager = null!;
            DateTimeProvider = null!;
            ServiceProvider = null!;
            NavigationManager = null!;
        }

        #endregion

        private ILogger? BuildLogger()
        {
            return ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<ComponentBase<TModel>>();
        }

        #region Properties

        protected internal TModel Model => _model ??= new TModel();

        protected internal bool IsLoading
            => _pendingOperationCancellationSource != null;

        protected internal bool IsLoaded
            => !IsLoading && _model != null;

        protected internal bool IsInitiallyLoaded { get; private set; }

        protected internal INotificationManager Notifications
            => _ambientOperationContext.Value?.Notifications ?? NotificationManager;

        private IEnumerable<ValidationResult> _validationResults = Enumerable.Empty<ValidationResult>();

        protected internal IEnumerable<ValidationResult> ValidationResults
        {
            get => _ambientOperationContext.Value?.ValidationResults ?? _validationResults;
            set
            {
                if (value is null)
                    throw new ArgumentNullException(nameof(value));

                if (_ambientOperationContext.Value != null)
                {
                    _ambientOperationContext.Value.ValidationResults = value;
                }
                else
                {
                    _validationResults = value;
                }
            }
        }

        [Inject] private NotificationManager NotificationManager { get; set; }
        [Inject] private IDateTimeProvider DateTimeProvider { get; set; }
        [Inject] private IServiceProvider ServiceProvider { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }

        #endregion

        protected sealed override void OnInitialized()
        {
            IsInitiallyLoaded = false;
            NavigationManager.LocationChanged += OnLocationChanged;
            Load();
            OnInitialized(false);
        }

        protected sealed override Task OnInitializedAsync()
        {
            return OnInitializedAsync(false);
        }

        protected virtual void OnInitialized(bool locationChanged) { }

        protected virtual Task OnInitializedAsync(bool locationChanged)
        {
            return Task.CompletedTask;
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs? e)
        {
            InvokeAsync(() => OnLocationChangedAsync()).HandleExceptions(Logger);
        }

        private ValueTask OnLocationChangedAsync()
        {
            Load();
            OnInitialized(true);

            var task = OnInitializedAsync(true);

            // If no async work is to be performed, i.e. the task has already ran to completion
            // or was canceled by the time we got to inspect it, avoid going async and re-invoking
            // StateHasChanged at the culmination of the async work.
            var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
                task.Status != TaskStatus.Canceled;

            // We always call StateHasChanged here as we want to trigger a rerender after OnParametersSet and
            // the synchronous part of OnParametersSetAsync has run.
            StateHasChanged();

            return shouldAwaitTask ?
                CallStateHasChangedOnAsyncCompletion(task) :
                default;
        }

        private async ValueTask CallStateHasChangedOnAsyncCompletion(Task task)
        {
            try
            {
                await task.ConfigureAwait(true);
            }
            catch // avoiding exception filters for AOT runtime support
            {
                // Ignore exceptions from task cancelletions, but don't bother issuing a state change.
                if (task.IsCanceled)
                {
                    return;
                }

                throw;
            }

            StateHasChanged();
        }

        protected virtual bool TryExtractModelAsync(
            IDispatchResult dispatchResult,
            [NotNullWhen(true)] out TModel? model)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            return dispatchResult.IsSuccessWithResult(out model);
        }

        #region Load model

        protected virtual ValueTask<TModel?> LoadModelAsync(CancellationToken cancellation)
        {
            return new ValueTask<TModel?>(result: null);
        }

        protected virtual async ValueTask<TModel?> EvaluateLoadResultAsync(IDispatchResult dispatchResult)
        {
            if (TryExtractModelAsync(dispatchResult, out var model))
            {
                return await OnLoadSuccessAsync(model, dispatchResult);
            }

            await EvaluateFailureResultAsync(dispatchResult);
            return null;
        }

        protected virtual ValueTask<TModel?> OnLoadSuccessAsync(TModel model, IDispatchResult dispatchResult)
        {
            return new ValueTask<TModel?>(model);
        }

        #endregion

        #region Store model

        protected virtual ValueTask<TModel?> StoreModelAsync(CancellationToken cancellation)
        {
            return new ValueTask<TModel?>(result: null);
        }

        protected virtual async ValueTask<TModel?> EvaluateStoreResultAsync(IDispatchResult dispatchResult)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            if (dispatchResult.IsSuccess)
            {
                if (!TryExtractModelAsync(dispatchResult, out var model))
                {
                    model = null;
                }

                return await OnStoreSuccessAsync(model, dispatchResult);
            }

            await EvaluateFailureResultAsync(dispatchResult);
            return null;
        }

        protected virtual async ValueTask<TModel?> OnStoreSuccessAsync(TModel? model, IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(
                NotificationType.Success,
                "Successfully performed operation.")
            {
                Expiration = DateTimeProvider.GetCurrentTime() + TimeSpan.FromSeconds(10)
            };

            Notifications.PlaceNotification(notification);
            return model ?? await LoadAsync();
        }

        #endregion

        #region Operation execution

        protected void Load()
        {
            LoadAsync().HandleExceptions(Logger);
        }

        protected async ValueTask<TModel?> LoadAsync(CancellationToken cancellation = default)
        {
            // We are already in a context. Skip operation setup.
            if (_ambientOperationContext.Value != null)
            {
                return await LoadModelAsync(cancellation);
            }

            var operationContext = new LoadOperationContext(this, cancellation);

            try
            {
                await operationContext.ExecuteOperationAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (operationContext.CancellationTokenSource.IsCancellationRequested && !cancellation.IsCancellationRequested)
            {
                if (_isDisposed)
                {
                    return null;
                }
            }

            // We do not pass the model from the commit operation back to us (the caller) 
            // because we may not actually succeed loading the model. 
            // We may be canceled by a concurrent load operation that's result we need to return here.
            return await _pendingOperationCompletion.Task.ConfigureAwait(true);
        }

        protected void Store()
        {
            StoreAsync().HandleExceptions(Logger);
        }

        protected async ValueTask<TModel?> StoreAsync(CancellationToken cancellation = default)
        {
            // We are already in a context. Skip operation setup.
            if (_ambientOperationContext.Value != null)
            {
                return await StoreModelAsync(cancellation);
            }

            var operationContext = new StoreOperationContext(this, cancellation);

            try
            {
                await operationContext.ExecuteOperationAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (operationContext.CancellationTokenSource.IsCancellationRequested && !cancellation.IsCancellationRequested)
            {
                if (_isDisposed)
                {
                    return null;
                }
            }

            // We do not pass the model from the commit operation back to us (the caller) 
            // because we may not actually succeed loading the model. 
            // We may be canceled by a concurrent load operation that's result we need to return here.
            return await _pendingOperationCompletion.Task.ConfigureAwait(true);
        }

        protected virtual void OnStored() { }

        protected virtual ValueTask OnStoredAsync()
        {
            return default;
        }

        protected virtual void OnLoaded() { }

        protected virtual ValueTask OnLoadedAsync()
        {
            return default;
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
            ValidationResults = (dispatchResult as ValidationFailureDispatchResult)
                ?.ValidationResults
                ?? Enumerable.Empty<ValidationResult>();

            var validationMessages = ValidationResults.Where(p => string.IsNullOrWhiteSpace(p.Member)).Select(p => p.Message);

            if (!validationMessages.Any())
            {
                validationMessages = Enumerable.Repeat(GetValidationMessage(dispatchResult), 1);
            }

            foreach (var validationMessage in validationMessages)
            {
                var alert = new NotificationMessage(NotificationType.Danger, validationMessage);
                Notifications.PlaceNotification(alert);
            }

            return default;
        }

        protected virtual ValueTask OnConcurrencyIssueAsync(IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(NotificationType.Danger, "A concurrency issue occured.");
            Notifications.PlaceNotification(notification);
            return default;
        }

        protected virtual ValueTask OnNotFoundAsync(IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(NotificationType.Info, "Not found.");
            Notifications.PlaceNotification(notification);
            return default;
        }

        protected virtual ValueTask OnNotAuthenticatedAsync(IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(NotificationType.Info, "Not authenticated.");
            Notifications.PlaceNotification(notification);
            return default;
        }

        protected virtual ValueTask OnNotAuthorizedAsync(IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(NotificationType.Info, "Not authorized.");
            Notifications.PlaceNotification(notification);
            return default;
        }

        protected virtual ValueTask OnFailureAsync(IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(NotificationType.Danger, "An unexpected error occured.");

            Notifications.PlaceNotification(notification);
            return default;
        }

        #endregion

        #region IDisposable

        private bool _isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;

                lock (_loadModelMutex)
                {
                    try
                    {
                        _pendingOperationCancellationSource?.Cancel();
                    }
                    catch (ObjectDisposedException) { }
                }

                if (NavigationManager != null)
                {
                    NavigationManager.LocationChanged -= OnLocationChanged;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        private abstract class OperationContext : IDisposable
        {
            private readonly INotificationRecorder _notifications;

            public OperationContext(
                ComponentBase<TModel> componentBase,
                CancellationToken cancellation)
            {
                _notifications = ComponentBase.NotificationManager.CreateRecorder();
                ValidationResults = ComponentBase._validationResults;
                ComponentBase = componentBase;

                // An operation is in progress currently.
                if (ComponentBase._pendingOperationCancellationSource != null)
                {
                    try
                    {
                        ComponentBase._pendingOperationCancellationSource.Cancel();
                    }
                    catch (ObjectDisposedException) { }
                }

                CancellationTokenSource = cancellation.CanBeCanceled
                                 ? CancellationTokenSource.CreateLinkedTokenSource(cancellation, default)
                                 : new CancellationTokenSource();

                ComponentBase._pendingOperationCancellationSource = CancellationTokenSource;
            }

            protected ComponentBase<TModel> ComponentBase { get; }

            public INotificationManager Notifications => _notifications;
            public IEnumerable<ValidationResult> ValidationResults { get; set; } = Enumerable.Empty<ValidationResult>();
            public CancellationTokenSource CancellationTokenSource { get; }

            protected abstract ValueTask<TModel?> ExcuteModelOperationAsync(CancellationToken cancellation);

            public async ValueTask ExecuteOperationAsync()
            {
                TModel? model = null;

                try
                {
                    // Set the ambient operation context.
                    ComponentBase._ambientOperationContext.Value = this;

                    try
                    {
                        model = await ExcuteModelOperationAsync(CancellationTokenSource.Token);
                    }
                    finally
                    {
                        // Reset the ambient alert message handler
                        ComponentBase._ambientOperationContext.Value = null;
                    }
                }
                finally
                {
                    await CommitLoadAsync(model);
                }
            }

            private async ValueTask CommitLoadAsync(TModel? model)
            {
                // We are running on a synchronization context, but we await on the result task of
                // OnModelLoadedAsync. Therefore, multiple invokations of CommitLoadOperationAsync
                // may actually run concurrently overriding the results of each other and
                // bringing concurrency to the derived component via concurrent calls to OnModelLoadedAsync.
                // We protect us be only executing only one CommitLoadOperationAsync call a time.

                using (await ComponentBase._loadModelMutex.LockAsync(CancellationTokenSource.Token))
                {
                    if (ComponentBase._pendingOperationCancellationSource != CancellationTokenSource)
                        return;

                    ComponentBase._pendingOperationCancellationSource = null;

                    ComponentBase._lastSuccessOperationContext?.Dispose();
                    ComponentBase._lastSuccessOperationContext = this;

                    await PublishAsync(model);
                }
            }

            private async ValueTask PublishAsync(TModel? model)
            {
                ComponentBase._validationResults = ValidationResults;
                _notifications.PublishNotifications();

                ComponentBase._model = model;
                ComponentBase._pendingOperationCompletion.SetResult(model);
                ComponentBase._pendingOperationCompletion = ValueTaskCompletionSource.Create<TModel?>();

                if (model != null)
                {
                    ComponentBase.IsInitiallyLoaded = true;

                    await OnCommitedAsync();
                    ComponentBase.StateHasChanged();
                }
            }

            protected abstract ValueTask OnCommitedAsync();

            public void Dispose()
            {
                _notifications.Dispose();
            }
        }

        private sealed class LoadOperationContext : OperationContext
        {
            public LoadOperationContext(
                ComponentBase<TModel> componentBase,
                CancellationToken cancellationToken)
                : base(componentBase, cancellationToken) { }

            protected override ValueTask<TModel?> ExcuteModelOperationAsync(CancellationToken cancellation)
            {
                return ComponentBase.LoadModelAsync(cancellation);
            }

            protected override async ValueTask OnCommitedAsync()
            {
                ComponentBase.OnLoaded();
                await ComponentBase.OnLoadedAsync();
            }
        }

        private sealed class StoreOperationContext : OperationContext
        {
            public StoreOperationContext(
                ComponentBase<TModel> componentBase,
                CancellationToken cancellationToken)
                : base(componentBase, cancellationToken) { }

            protected override ValueTask<TModel?> ExcuteModelOperationAsync(CancellationToken cancellation)
            {
                return ComponentBase.StoreModelAsync(cancellation);
            }

            protected override async ValueTask OnCommitedAsync()
            {
                ComponentBase.OnStored();
                await ComponentBase.OnStoredAsync();

                ComponentBase.OnLoaded();
                await ComponentBase.OnLoadedAsync();
            }
        }
    }
}
