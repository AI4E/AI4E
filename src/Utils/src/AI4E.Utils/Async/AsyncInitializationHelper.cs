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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Async
{
#pragma warning disable CA1001 
    public readonly struct AsyncInitializationHelper : IAsyncInitialization, IEquatable<AsyncInitializationHelper>
#pragma warning restore CA1001
    {
        private readonly Task? _initialization; // This is null in case of a default struct value
        private readonly CancellationTokenSource? _cancellation;

        public AsyncInitializationHelper(Func<CancellationToken, Task> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            this = default;

            _cancellation = new CancellationTokenSource();
            _initialization = InitInternalAsync(initialization);
        }

        public AsyncInitializationHelper(Func<Task> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            this = default;

            _cancellation = null;
            _initialization = InitInternalAsync(initialization);
        }

        internal AsyncInitializationHelper(Task? initialization, CancellationTokenSource? cancellation)
        {
            _initialization = initialization;
            _cancellation = cancellation;
        }

        private async Task InitInternalAsync(Func<CancellationToken, Task> initialization)
        {
            Debug.Assert(_cancellation != null);

            await Task.Yield();

            try
            {
                await initialization(_cancellation!.Token).ConfigureAwait(false);
            }
            finally
            {
                _cancellation!.Dispose();
            }
        }

        private async Task InitInternalAsync(Func<Task> initialization)
        {
            await Task.Yield();
            await initialization().ConfigureAwait(false);
        }

        public Task Initialization => _initialization ?? Task.CompletedTask;

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        public async Task<bool> CancelAsync()
        {
            Cancel();

            try
            {
                await Initialization.ConfigureAwait(false);
                return true;
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool Equals(AsyncInitializationHelper other)
        {
            return other._initialization == _initialization;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AsyncInitializationHelper asyncInitializationHelper
                && Equals(asyncInitializationHelper);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _initialization?.GetHashCode() ?? 0;
        }

        public static bool operator ==(AsyncInitializationHelper left, AsyncInitializationHelper right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AsyncInitializationHelper left, AsyncInitializationHelper right)
        {
            return !left.Equals(right);
        }
    }

#pragma warning disable CA1001
    public readonly struct AsyncInitializationHelper<T> : IAsyncInitialization, IEquatable<AsyncInitializationHelper<T>>
#pragma warning restore CA1001
    {
        private readonly Task<T>? _initialization; // This is null in case of a default struct value
        private readonly CancellationTokenSource? _cancellation;

        public AsyncInitializationHelper(Func<CancellationToken, Task<T>> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            this = default;

            _cancellation = new CancellationTokenSource();
            _initialization = InitInternalAsync(initialization);
        }

        public AsyncInitializationHelper(Func<Task<T>> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            this = default;

            _cancellation = null;
            _initialization = InitInternalAsync(initialization);
        }

        public Task<T> Initialization => _initialization ?? Task.FromResult<T>(default!); // TODO: We may not return null here!

        private async Task<T> InitInternalAsync(Func<CancellationToken, Task<T>> initialization)
        {
            Debug.Assert(_cancellation != null);

            await Task.Yield();

            try
            {
                return await initialization(_cancellation!.Token).ConfigureAwait(false);
            }
            finally
            {
                _cancellation!.Dispose();
            }
        }

        private async Task<T> InitInternalAsync(Func<Task<T>> initialization)
        {
            await Task.Yield();

            return await initialization().ConfigureAwait(false);
        }

        Task IAsyncInitialization.Initialization => Initialization;

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        public async Task<(bool success, T result)> CancelAsync()
        {
            Cancel();

            try
            {
                var result = await Initialization.ConfigureAwait(false);
                return (true, result);
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                return (false, default);
            }
        }

        public static implicit operator AsyncInitializationHelper(AsyncInitializationHelper<T> source)
        {
            return source.ToAsyncInitializationHelper();
        }

        public AsyncInitializationHelper ToAsyncInitializationHelper()
        {
            return new AsyncInitializationHelper(_initialization, _cancellation);
        }

        /// <inheritdoc />
        public bool Equals(AsyncInitializationHelper<T> other)
        {
            return other._initialization == _initialization;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AsyncInitializationHelper asyncInitializationHelper
                && Equals(asyncInitializationHelper);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _initialization?.GetHashCode() ?? 0;
        }

        public static bool operator ==(AsyncInitializationHelper<T> left, AsyncInitializationHelper<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AsyncInitializationHelper<T> left, AsyncInitializationHelper<T> right)
        {
            return !left.Equals(right);
        }
    }
}
