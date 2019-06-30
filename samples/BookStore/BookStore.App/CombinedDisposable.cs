using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace BookStore.App
{
    public sealed class CombinedDisposable : IDisposable
    {
        private readonly ImmutableList<IDisposable> _disposables;

        public CombinedDisposable(IEnumerable<IDisposable> disposables)
        {
            if (disposables is null)
                throw new ArgumentNullException(nameof(disposables));

            _disposables = disposables as ImmutableList<IDisposable> ??
                          (disposables as ImmutableList<IDisposable>.Builder)?.ToImmutable() ??
                           disposables.ToImmutableList();

            if (_disposables.Any(p => p is null))
            {
                throw new ArgumentException(
                    "The collection must not contain null entries.",
                    nameof(disposables));
            }
        }

        public CombinedDisposable(params IDisposable[] disposables)
            : this((IEnumerable<IDisposable>)disposables)
        { }

        public void Dispose()
        {
            Exception exception = null;
            List<Exception> exceptions = null;

            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception exc)
                {
                    // If exceptions is not null, exception has to be null.
                    Debug.Assert(exceptions == null || exception == null);

                    if (exception is null && exceptions is null)
                    {
                        exception = exc;
                    }
                    else if (exception is null)
                    {
                        exceptions.Add(exc);
                    }
                    else
                    {
                        exceptions = new List<Exception> { exception, exc };
                        exception = null;
                    }
                }
            }

            // If exceptions is not null, exception has to be null.
            Debug.Assert(exception == null || exceptions == null);

            if (!(exception is null))
            {
                throw exception;
            }

            if (!(exceptions is null))
            {
                throw new AggregateException(exceptions);
            }
        }
    }
}
