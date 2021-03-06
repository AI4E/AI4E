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
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Notifications;
using AI4E.Messaging;
using AI4E.Messaging.Validation;
using AI4E.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

// TODO: Validate, Store and Load operations currently cannot overlap each other. Is this inteded?
// TODO: Loads can starve if load request happpens more frequently then the completion of the load operations 
//       within a sufficient small amount of time.

namespace AI4E.AspNetCore.Components
{
    /// <summary>
    /// A base class for components that presents a model.
    /// </summary>
    /// <typeparam name="TModel">The type of model the component presents.</typeparam>
    public abstract class ComponentBase<TModel> : ComponentBase, IDisposable
        where TModel : class, new()
    {
        #region Fields

        private readonly AsyncLocal<OperationContext?> _ambientOperationContext;
        private readonly Lazy<ILogger?> _logger;
        private readonly AsyncLock _loadModelMutex = new AsyncLock();
        private ILogger? Logger => _logger.Value;
#pragma warning disable IDE0044 
        private TModel? _model;
        private TModel _nonNullModel = new TModel();
#pragma warning disable IDE0069, CA2213
        private OperationContext? _lastSuccessOperationContext;
        private CancellationTokenSource? _pendingOperationCancellationSource;
        private TaskCompletionSource<TModel?> _pendingOperationCompletion = new TaskCompletionSource<TModel?>();
#pragma warning restore IDE0069, CA2213
#pragma warning restore IDE0044

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="ComponentBase{TModel}"/> type in a derived class.
        /// </summary>
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

        /// <summary>
        /// Gets the model to present.
        /// </summary>
        protected internal TModel Model
            => _ambientOperationContext.Value?.Model ?? _model ?? _nonNullModel;

        /// <summary>
        /// Gets a boolean value indicating whether a model load operation is in progress.
        /// </summary>
        protected internal bool IsLoading
            => _pendingOperationCancellationSource != null;

        /// <summary>
        /// Gets a boolean value indicating whether the model loaded successfully.
        /// </summary>
        protected internal bool IsLoaded
            => !IsLoading && _model != null;

        /// <summary>
        /// Gets a boolean value indicating whether the model is loaded initially successfully.
        /// </summary>
        protected internal bool IsInitiallyLoaded { get; private set; }

        /// <summary>
        /// Gets the notification manager for the current context.
        /// </summary>
        protected internal INotificationManager Notifications
            => _ambientOperationContext.Value?.Notifications ?? NotificationManager;

        private IEnumerable<ValidationResult> _validationResults = Enumerable.Empty<ValidationResult>();

        /// <summary>
        /// Gets or sets the validation results for the current context.
        /// </summary>
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

        /// <summary>
        /// Gets or sets a boolean value indicating whether a load operation shall be performed 
        /// after a successful store.
        /// </summary>
        protected internal bool EnableLoadAfterStore { get; set; } = true;

        /// <summary>
        /// Gets pr sets a boolean value indicating whether a load operation shall be performed 
        /// when a redirect to the current component is executed.
        /// </summary>
        protected internal bool EnableLoadOnRedirect { get; set; } = true;

        [Inject] private NotificationManager NotificationManager { get; set; }
        [Inject] private IDateTimeProvider DateTimeProvider { get; set; }
        [Inject] private IServiceProvider ServiceProvider { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }

        #endregion

        /// <inheritdoc/>
        protected sealed override void OnInitialized()
        {
            IsInitiallyLoaded = false;
            NavigationManager.LocationChanged += OnLocationChanged;
            _ = LoadAsync();
            OnInitialized(false);
        }

        /// <inheritdoc/>
        protected sealed override Task OnInitializedAsync()
        {
            return OnInitializedAsync(false);
        }

        /// <summary>
        /// Method invoked when the component is ready to start, having received its initial
        /// parameters from its parent in the render tree.
        /// </summary>
        /// <param name="locationChanged">
        /// A boolean value indicating whether the method is called due to a redirect to the current component.
        /// </param>
        protected virtual void OnInitialized(bool locationChanged) { }

        /// <summary>
        /// Method invoked when the component is ready to start, having received its initial
        /// parameters from its parent in the render tree. Override this method if you will
        /// perform an asynchronous operation and want the component to refresh when that
        /// </summary>
        /// <param name="locationChanged">
        /// A boolean value indicating whether the method is called due to a redirect to the current component.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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
            if (EnableLoadOnRedirect)
            {
                _ = LoadAsync();
            }

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

        /// <summary>
        /// Tries to extract the model from the specified dispach result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result that contains the model.</param>
        /// <param name="model">Cotnains the model if can be extracted.</param>
        /// <returns>True if the model can be extracted, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
        protected virtual bool TryExtractModelAsync(
            IDispatchResult dispatchResult,
            [NotNullWhen(true)] out TModel? model)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            return dispatchResult.IsSuccessWithResult(out model) && model != null;
        }

        #region Load model

        /// <summary>
        /// Asynchronously loads the model.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the loaded model, or <c>null</c> if the model cannot be loaded.
        /// </returns>
        protected virtual ValueTask<TModel?> LoadModelAsync(CancellationToken cancellation)
        {
            return new ValueTask<TModel?>(result: null);
        }

        /// <summary>
        /// Asynchronously evaluates the specified load result.
        /// </summary>
        /// <param name="dispatchResult">The load result.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the extracted model, or <c>null</c> if the model cannot be loaded.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
        protected virtual async ValueTask<TModel?> EvaluateLoadResultAsync(IDispatchResult dispatchResult)
        {
            if (TryExtractModelAsync(dispatchResult, out var model))
            {
                return await OnLoadSuccessAsync(model, dispatchResult);
            }

            await EvaluateFailureResultAsync(dispatchResult);
            return null;
        }

        /// <summary>
        /// Asynchronously post-processes a successfully loaded model.
        /// </summary>
        /// <param name="model">The loaded model.</param>
        /// <param name="dispatchResult">The load result.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the extracted model, or <c>null</c> if the model cannot be loaded.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="model"/> or <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
        protected virtual ValueTask<TModel?> OnLoadSuccessAsync(TModel model, IDispatchResult dispatchResult)
        {
            return new ValueTask<TModel?>(model);
        }

        #endregion

        #region Store model

        /// <summary>
        /// Asynchronously stores the model and returns the updated model.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous opeeration.
        /// When evaluated, the tasks result contains the updated model, or <c>null</c> if the model cannot be updated.
        /// </returns>
        protected virtual ValueTask<TModel?> StoreModelAsync(CancellationToken cancellation)
        {
            return new ValueTask<TModel?>(result: null);
        }

        /// <summary>
        /// Asynchronously evaluates the specified store result.
        /// </summary>
        /// <param name="dispatchResult">The store result.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the extracted model, or <c>null</c> if the model cannot be loaded.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
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

                NotifySuccess();
                return await OnStoreSuccessAsync(model, dispatchResult);
            }

            NotifyFailure();
            await EvaluateFailureResultAsync(dispatchResult);
            return Model;
        }

        /// <summary>
        /// Asynchronously post-processes an upated model after a successful store.
        /// </summary>
        /// <param name="model">The loaded model.</param>
        /// <param name="dispatchResult">The load result.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the extracted model, or <c>null</c> if the model cannot be loaded.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
        protected virtual async ValueTask<TModel?> OnStoreSuccessAsync(TModel? model, IDispatchResult dispatchResult)
        {
            var notification = new NotificationMessage(
                NotificationType.Success,
                "Successfully performed operation.")
            {
                Expiration = DateTimeProvider.GetCurrentTime() + TimeSpan.FromSeconds(10)
            };

            Notifications.PlaceNotification(notification);
            return model ?? (EnableLoadAfterStore ? await LoadAsync().ConfigureAwait(true) : Model);
        }

        #endregion

        #region Validate model

        /// <summary>
        /// Asynchronously validates the model.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous opeeration.
        /// </returns>
        protected virtual ValueTask ValidateModelAsync(CancellationToken cancellation)
        {
            return default;
        }

        /// <summary>
        /// Asynchronously evaluates the specified validate result.
        /// </summary>
        /// <param name="dispatchResult">The validate result.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
        protected virtual async ValueTask EvaluateValidateResultAsync(IDispatchResult dispatchResult)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            if (dispatchResult.IsSuccess)
            {
                await OnValidateSuccessAsync(dispatchResult);
            }

            await EvaluateFailureResultAsync(dispatchResult);
        }

        /// <summary>
        /// Asynchronously post-processes the validation results after a successful validation.
        /// </summary>
        /// <param name="dispatchResult">The validate result.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchResult"/> is <c>null</c>.
        /// </exception>
        protected virtual ValueTask OnValidateSuccessAsync(IDispatchResult dispatchResult)
        {
            ValidationResults = Enumerable.Empty<ValidationResult>();
            return default;
        }

        #endregion

        #region Operation execution

        /// <summary>
        /// Asynchronously performs a model load operation.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the loaded model.
        /// </returns>
        protected Task<TModel?> LoadAsync()
        {
            return LoadAsync(default);
        }

        /// <summary>
        /// Asynchronously performs a model load operation.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the loaded model.
        /// </returns>
        protected async Task<TModel?> LoadAsync(CancellationToken cancellation)
        {
            // We are already in a context. Skip operation setup.
            if (_ambientOperationContext.Value != null)
            {
                return await LoadModelAsync(cancellation);
            }

#pragma warning disable CA2000, IDE0067
            var operationContext = new LoadOperationContext(this, cancellation);
#pragma warning restore CA2000, IDE0067

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

        /// <summary>
        /// Asynchronously performs a model store operation.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the updated model.
        /// </returns>
        protected Task<TModel?> StoreAsync()
        {
            return StoreAsync(default);
        }

        /// <summary>
        /// Asynchronously performs a model store operation.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the updated model.
        /// </returns>
        protected async Task<TModel?> StoreAsync(CancellationToken cancellation)
        {
            // We are already in a context. Skip operation setup.
            if (_ambientOperationContext.Value != null)
            {
                return await StoreModelAsync(cancellation);
            }

#pragma warning disable CA2000, IDE0067
            var operationContext = new StoreOperationContext(this, cancellation);
#pragma warning restore CA2000, IDE0067

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

        /// <summary>
        /// Asynchronously performs a model validate operation.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        protected Task ValidateAsync()
        {
            return ValidateAsync(default);
        }

        /// <summary>
        /// Asynchronously performs a model validate operation.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        protected async Task ValidateAsync(CancellationToken cancellation)
        {
            // We are already in a context. Skip operation setup.
            if (_ambientOperationContext.Value != null)
            {
                await ValidateModelAsync(cancellation);
                return;
            }

#pragma warning disable CA2000, IDE0067
            var operationContext = new ValidateOperationContext(this, cancellation);
#pragma warning restore CA2000, IDE0067

            try
            {
                await operationContext.ExecuteOperationAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (operationContext.CancellationTokenSource.IsCancellationRequested && !cancellation.IsCancellationRequested)
            {
                if (_isDisposed)
                {
                    return;
                }
            }

            // We do not pass the model from the commit operation back to us (the caller) 
            // because we may not actually succeed loading the model. 
            // We may be canceled by a concurrent load operation that's result we need to return here.
            await _pendingOperationCompletion.Task.ConfigureAwait(true);
        }

        /// <summary>
        /// Method invoked when the component has validated the model.
        /// </summary>
        protected virtual void OnValidated() { }

        /// <summary>
        /// Method invoked when the component has validated the model.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        protected virtual ValueTask OnValidatedAsync()
        {
            return default;
        }

        /// <summary>
        /// Method invoked when the component has stored the model.
        /// </summary>
        protected virtual void OnStored() { }

        /// <summary>
        /// Method invoked when the component has stored the model.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        protected virtual ValueTask OnStoredAsync()
        {
            return default;
        }

        /// <summary>
        /// Method invoked when the component has loaded the model.
        /// </summary>
        protected virtual void OnLoaded() { }

        /// <summary>
        /// Method invoked when the component has loaded the model.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">A boolean value indicating whether the component is disposing.</param>
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

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        /// <summary>
        /// Norifies an operation failure status.
        /// </summary>
        protected internal void NotifyFailure()
        {
            NotifyStatus(false);
        }

        /// <summary>
        /// Norifies an operation success status.
        /// </summary>
        protected internal void NotifySuccess()
        {
            NotifyStatus(true);
        }

        private void NotifyStatus(bool success)
        {
            var context = _ambientOperationContext.Value;

            if (context != null)
            {
                context.IsSuccess = success;
            }
        }

        private abstract class OperationContext : IDisposable
        {
            private readonly INotificationRecorder _notifications;

            public OperationContext(
                ComponentBase<TModel> component,
                CancellationToken cancellation)
            {
                Component = component;
                _notifications = Component.NotificationManager.CreateRecorder();
                ValidationResults = Component._validationResults;
                Model = Component.Model;

                // An operation is in progress currently.
                if (Component._pendingOperationCancellationSource != null)
                {
                    try
                    {
                        Component._pendingOperationCancellationSource.Cancel();
                    }
                    catch (ObjectDisposedException) { }
                }

                CancellationTokenSource = cancellation.CanBeCanceled
                                 ? CancellationTokenSource.CreateLinkedTokenSource(cancellation, default)
                                 : new CancellationTokenSource();

                Component._pendingOperationCancellationSource = CancellationTokenSource;
            }

            protected ComponentBase<TModel> Component { get; }

            public INotificationManager Notifications => _notifications;
            public IEnumerable<ValidationResult> ValidationResults { get; set; } = Enumerable.Empty<ValidationResult>();
            public CancellationTokenSource CancellationTokenSource { get; }
            public TModel Model { get; }
            public bool IsSuccess { get; set; } = true;

            protected abstract ValueTask<TModel?> ExcuteModelOperationAsync(CancellationToken cancellation);

            public async ValueTask ExecuteOperationAsync()
            {
                TModel? model = null;

                try
                {
                    // Set the ambient operation context.
                    Component._ambientOperationContext.Value = this;

                    try
                    {
                        model = await ExcuteModelOperationAsync(CancellationTokenSource.Token);
                    }
                    finally
                    {
                        // Reset the ambient alert message handler
                        Component._ambientOperationContext.Value = null;
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

                using (await Component._loadModelMutex.LockAsync(CancellationTokenSource.Token))
                {
                    if (Component._pendingOperationCancellationSource != CancellationTokenSource)
                        return;

                    Component._pendingOperationCancellationSource = null;

                    Component._lastSuccessOperationContext?.Dispose();
                    Component._lastSuccessOperationContext = this;

                    await PublishAsync(model);
                }
            }

            private async ValueTask PublishAsync(TModel? model)
            {
                Component._validationResults = ValidationResults;
                _notifications.PublishNotifications();

                if (CommitModel(model))
                {
                    Component._model = model;
                    Component._nonNullModel = model ?? Component._model ?? new TModel();
                    Component._pendingOperationCompletion.SetResult(model);
                    Component._pendingOperationCompletion = new TaskCompletionSource<TModel?>();
                }

                await OnCommittedAsync(model);

                Component.StateHasChanged();
            }

            protected virtual bool CommitModel(TModel? model)
            {
                return true;
            }

            protected abstract ValueTask OnCommittedAsync(TModel? model);

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
                return Component.LoadModelAsync(cancellation);
            }

            protected override async ValueTask OnCommittedAsync(TModel? model)
            {
                if (model != null && !ReferenceEquals(model, Model))
                {
                    Component.IsInitiallyLoaded = true;

                    Component.OnLoaded();
                    await Component.OnLoadedAsync();
                }
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
                return Component.StoreModelAsync(cancellation);
            }

            protected override async ValueTask OnCommittedAsync(TModel? model)
            {
                // We are at the end of our commit step. The store operation may have been executed
                // - successfully and yield a new model (This indicated that we performed an implicit or explicit model load)
                // - successfully and yield no new model
                // - successfully and yield a null model (For example for deletion operations.)
                // - non successfully (Via the IsSuccess state)

                Component.OnValidated();
                await Component.OnValidatedAsync();

                if (IsSuccess)
                {
                    Component.OnStored();
                    await Component.OnStoredAsync();
                }

                if (model != null && !ReferenceEquals(model, Model))
                {
                    Component.IsInitiallyLoaded = true;

                    Component.OnLoaded();
                    await Component.OnLoadedAsync();
                }
            }
        }

        private sealed class ValidateOperationContext : OperationContext
        {
            public ValidateOperationContext(
                ComponentBase<TModel> componentBase,
                CancellationToken cancellationToken)
                : base(componentBase, cancellationToken) { }

            protected override async ValueTask<TModel?> ExcuteModelOperationAsync(CancellationToken cancellation)
            {
                await Component.ValidateModelAsync(cancellation);
                return null; // Just return anything. Theses value will never get committed back.
            }

            protected override bool CommitModel(TModel? model)
            {
                return false;
            }

            protected override async ValueTask OnCommittedAsync(TModel? model)
            {
                Component.OnValidated();
                await Component.OnValidatedAsync();
            }
        }
    }
}
