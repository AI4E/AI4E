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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Async
{
    public sealed class AsyncLifetimeManager : IAsyncInitialization, IAsyncDisposable, IDisposable
    {
        private readonly DisposableAsyncLazy<byte> _underlyingManager;

        #region C'tor

        public AsyncLifetimeManager(Func<CancellationToken, Task> initialization, Func<Task> disposal, bool executeOnCallingThread = true)
        {
            var options = GetOptions(executeOnCallingThread);
            _underlyingManager = new DisposableAsyncLazy<byte>(
                factory: AsFactory(initialization),
                disposal: AsDisposal(disposal),
                options);
        }

        public AsyncLifetimeManager(Func<Task> initialization, Func<Task> disposal, bool executeOnCallingThread = true)
        {
            var options = GetOptions(executeOnCallingThread);
            _underlyingManager = new DisposableAsyncLazy<byte>(
                factory: AsFactory(initialization),
                disposal: AsDisposal(disposal),
                options);
        }

        public AsyncLifetimeManager(Func<CancellationToken, Task> initialization, bool executeOnCallingThread = true)
        {
            var options = GetOptions(executeOnCallingThread);
            _underlyingManager = new DisposableAsyncLazy<byte>(
                factory: AsFactory(initialization),
                options);
        }

        public AsyncLifetimeManager(Func<Task> initialization, bool executeOnCallingThread = true)
        {
            var options = GetOptions(executeOnCallingThread);
            _underlyingManager = new DisposableAsyncLazy<byte>(
                factory: AsFactory(initialization),
                options);
        }

        #endregion

        #region Helpers

        private static DisposableAsyncLazyOptions GetOptions(bool executeOnCallingThread)
        {
            var options = DisposableAsyncLazyOptions.Autostart;

            if (executeOnCallingThread)
            {
                options |= DisposableAsyncLazyOptions.ExecuteOnCallingThread;
            }

            return options;
        }

        private static Func<CancellationToken, Task<byte>> AsFactory(Func<Task> initialization)
        {
            return async cancellation =>
            {
                await initialization().ConfigureAwait(false);
                return 0;
            };
        }

        private static Func<CancellationToken, Task<byte>> AsFactory(Func<CancellationToken, Task> initialization)
        {
            return async cancellation =>
            {
                await initialization(cancellation).ConfigureAwait(false);
                return 0;
            };
        }

        private static Func<byte, Task> AsDisposal(Func<Task> disposal)
        {
            return _ => disposal();
        }

        #endregion

        public Task Initialization => _underlyingManager.Task;

        #region Disposal

        public Task Disposal => _underlyingManager.Disposal;

        public void Dispose()
        {
            _underlyingManager.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _underlyingManager.DisposeAsync();
        }

        #endregion
    }
}
