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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Notifications;
using AI4E.Messaging;
using AI4E.Messaging.Validation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.AspNetCore.Components
{
    public abstract class ComponentBase<TModel> : ComponentBase, IDisposable
        where TModel : class
    {
        #region Fields

        private readonly AsyncLocal<INotificationManager?> _ambientNotifications;
        private readonly Lazy<ILogger?> _logger;
        private readonly AsyncLock _loadModelMutex = new AsyncLock();
        private INotificationManagerScope? _loadModelNotifications;
        private ILogger? Logger => _logger.Value;
#pragma warning disable IDE0069, CA2213
        // If _loadModelCancellationSource is null, no operation is in progress currently.
        private CancellationTokenSource? _loadModelCancellationSource;
#pragma warning restore IDE0069, CA2213

        #endregion

        #region C'tor

        protected ComponentBase()
        {
            _ambientNotifications = new AsyncLocal<INotificationManager?>();
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

        protected internal TModel? Model { get; private set; }

        protected internal bool IsLoading
            => _loadModelCancellationSource != null;

        protected internal bool IsLoaded
            => !IsLoading && Model != null;

        protected internal bool IsInitiallyLoaded { get; private set; }

        protected internal INotificationManager Notifications
            => _ambientNotifications.Value ?? NotificationManager;

        [Inject] private NotificationManager NotificationManager { get; set; }
        [Inject] private Utils.IDateTimeProvider DateTimeProvider { get; set; }
        [Inject] private IServiceProvider ServiceProvider { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }

        #endregion

        protected sealed override void OnInitialized()
        {
            IsInitiallyLoaded = false;
            NavigationManager.LocationChanged += OnLocationChanged;
            LoadModel();
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
            LoadModel();
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

        #region Loading

        protected void LoadModel()
        {
            // An operation is in progress currently.
            if (_loadModelCancellationSource != null)
            {
                try
                {
                    _loadModelCancellationSource.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            _loadModelCancellationSource = new CancellationTokenSource();
            InternalLoadModelProtectedAsync(_loadModelCancellationSource)
                .HandleExceptions(Logger);
        }

        private async Task InternalLoadModelProtectedAsync(CancellationTokenSource cancellationSource)
        {
            using (cancellationSource)
            {
                try
                {
                    await InternalLoadModelAsync(cancellationSource)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested) { }
            }
        }

        private async Task InternalLoadModelAsync(CancellationTokenSource cancellationSource)
        {
            TModel? model = null;
            var notifications = NotificationManager.CreateRecorder();

            try
            {
                // Set the ambient alert message handler
                _ambientNotifications.Value = notifications;

                try
                {
                    model = await LoadModelAsync(cancellationSource.Token);
                }
                finally
                {
                    // Reset the ambient alert message handler
                    _ambientNotifications.Value = null;
                }
            }
            finally
            {
                await CommitLoadOperationAsync(cancellationSource, model, notifications);
            }
        }

        private async ValueTask CommitLoadOperationAsync(
            CancellationTokenSource cancellationSource,
            TModel? model,
            NotificationRecorder notifications)
        {
            // We are running on a synchronization context, but we await on the result task of
            // OnModelLoadedAsync. Therefore, multiple invokations of CommitLoadOperationAsync
            // may actually run concurrently overriding the results of each other and
            // bringing concurrency to the derived component via concurrent calls to OnModelLoadedAsync.
            // We protect us be only executing only one CommitLoadOperationAsync call a time.

            using (await _loadModelMutex.LockAsync(cancellationSource.Token))
            {
                if (_loadModelCancellationSource != cancellationSource)
                    return;

                _loadModelCancellationSource = null;

                _loadModelNotifications?.Dispose();
                _loadModelNotifications = notifications;
                notifications.PublishNotifications();

                if (model != null)
                {
                    IsInitiallyLoaded = true;
                    Model = model;

                    OnModelLoaded();
                    await OnModelLoadedAsync();
                    StateHasChanged();
                }
            }
        }

        protected virtual ValueTask<TModel?> LoadModelAsync(CancellationToken cancellation)
        {
            try
            {
                return new ValueTask<TModel?>(Activator.CreateInstance<TModel>());
            }
            catch (MissingMethodException exc)
            {
                throw new InvalidOperationException(
                    $"Cannot create a model of type {typeof(TModel)}. The type does not have a public default constructor.", exc);
            }
        }

        protected virtual async ValueTask<TModel?> EvaluateLoadResultAsync(IDispatchResult dispatchResult)
        {
            if (IsSuccess(dispatchResult, out var model))
            {
                await OnLoadSuccessAsync(model, dispatchResult);
                return model;
            }

            await EvaluateFailureResultAsync(dispatchResult);
            return null;
        }

        protected virtual ValueTask OnLoadSuccessAsync(TModel model, IDispatchResult dispatchResult)
        {
            return default;
        }

        protected virtual bool IsSuccess(IDispatchResult dispatchResult, out TModel model)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            return dispatchResult.IsSuccessWithResult(out model);
        }

        protected virtual void OnModelLoaded() { }

        protected virtual ValueTask OnModelLoadedAsync()
        {
            return default;
        }

        #endregion

        #region Store

        protected virtual ValueTask EvaluateStoreResultAsync(IDispatchResult dispatchResult)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            if (dispatchResult.IsSuccess)
            {
                return OnStoreSuccessAsync(Model, dispatchResult);
            }

            return EvaluateFailureResultAsync(dispatchResult);
        }

        protected virtual ValueTask OnStoreSuccessAsync(TModel? model, IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(
                NotificationType.Success,
                "Successfully performed operation.")
            {
                Expiration = DateTimeProvider.GetCurrentTime() + TimeSpan.FromSeconds(10)
            };

            Notifications.PlaceNotification(notification);
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_loadModelMutex)
                {
                    try
                    {
                        _loadModelCancellationSource?.Cancel();
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
    }
}
