using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using AI4E.Utils;
using BookStore.Alerts;
using Microsoft.AspNetCore.Components;

namespace BookStore.App
{
    // TODO: Naming Query vs. Load, Command vs. Store

    public abstract class ComponentBase<TModel> : ComponentBase, IDisposable
    {
        [Inject] protected internal IMessageDispatcher MessageDispatcher { get; private set; }
        [Inject] protected internal IAlertMessageManager AlertMessageManager { get; private set; }

        protected override void OnInit()
        {
            LoadModel();
        }

        #region Load

        private Task _loadModelOperation;
        private CancellationTokenSource _loadModelCancellation;
        private IAlertMessageManagerScope _loadModelAlerts;
        private readonly object _loadModelMutex = new object();

        protected internal TModel Model { get; private set; }
        protected internal bool IsLoading => Volatile.Read(ref _loadModelOperation) != null;

        protected void LoadModel()
        {
            lock (_loadModelMutex)
            {
                if (_loadModelAlerts == null)
                {
                    _loadModelAlerts = AlertMessageManager.CreateScope();
                }

                if (_loadModelOperation != null)
                {
                    _loadModelCancellation.Cancel();
                    _loadModelCancellation.Dispose();
                    _loadModelOperation.HandleExceptions();
                }

                _loadModelCancellation = new CancellationTokenSource();
                _loadModelOperation = InternalLoadModelAsync(_loadModelCancellation);
            }
        }

        private async Task InternalLoadModelAsync(CancellationTokenSource loadModelCancellation)
        {
            var cancellation = loadModelCancellation.Token;

            try
            {
                var model = await LoadModelAsync(cancellation);

                lock (_loadModelMutex)
                {
                    if (_loadModelCancellation != loadModelCancellation)
                        return;

                    // Clear messages from last model load
                    _loadModelAlerts?.ClearAlerts();

                    Model = model;
                    _loadModelOperation = null;
                    _loadModelCancellation.Dispose();
                }

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
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }

        protected virtual async ValueTask<TModel> LoadModelAsync(CancellationToken cancellation)
        {
            var dispatchResult = await MessageDispatcher.QueryAsync<TModel>(cancellation);
            return EvaluateQueryResult(dispatchResult);
        }

        protected virtual TModel EvaluateQueryResult(IDispatchResult queryResult)
        {
            if (queryResult.IsSuccessWithResult<TModel>(out var model))
            {
                return model;
            }
            else
            {
                // TODO: How can we attach alerts here and ensure consistency?
                return default;
            }
        }

        protected virtual void OnModelLoaded() { }

        protected virtual Task OnModelLoadedAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Store

        protected virtual void EvaluateCommandResult(IDispatchResult commandResult)
        {
            if (commandResult.IsSuccess)
            {
                return;
            }
            else
            {
                Console.WriteLine(commandResult.Message);

                // TODO: Show error
            }
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
