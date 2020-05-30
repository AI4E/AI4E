/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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

namespace Microsoft.Extensions.ObjectPool
{
    /// <summary>
    /// Provides extensions for the <see cref="ObjectPool{T}"/> type.
    /// </summary>
    public static class ObjectPoolExtension
    {
        /// <summary>
        /// Rent an object from the pool and returns an object that can be used to return the object.
        /// </summary>
        /// <typeparam name="T">The type of object to rent.</typeparam>
        /// <param name="objectPool">The object pool.</param>
        /// <param name="obj">Contains the rented object.</param>
        /// <returns>A instance of type <see cref="PooledObjectReturner{T}"/> that can be used to return the rented object to the pool.</returns>
#pragma warning disable CA1720
        public static PooledObjectReturner<T> Get<T>(this ObjectPool<T> objectPool, out T obj)
#pragma warning restore CA1720
            where T : class
        {
            if (objectPool == null)
                throw new ArgumentNullException(nameof(objectPool));

            obj = objectPool.Get();

            return new PooledObjectReturner<T>(objectPool, obj);
        }
    }

    internal sealed class PooledObjectReturnerSource
    {
        private static readonly ObjectPool<PooledObjectReturnerSource> _pool
            = new DefaultObjectPool<PooledObjectReturnerSource>(new PooledObjectReturnerSourcePooledObjectPolicy());

        public int Token { get; private set; } = 0;

        public bool IsDisposed(int token)
        {
            return Token != token;
        }

        public void Dispose(int token)
        {
            if (token == Token)
            {
                Token++;
                _pool.Return(this);
            }
        }

        public static PooledObjectReturnerSource Allocate()
        {
            return _pool.Get();
        }

        private sealed class PooledObjectReturnerSourcePooledObjectPolicy : IPooledObjectPolicy<PooledObjectReturnerSource>
        {
            public PooledObjectReturnerSource Create()
            {
                return new PooledObjectReturnerSource();
            }

            public bool Return(PooledObjectReturnerSource obj)
            {
                return obj.Token != 0;
            }
        }
    }

    /// <summary>
    /// Represents a pooled object returner that returns the object to the pool when disposed.
    /// </summary>
    /// <typeparam name="T">The type of pooled object.</typeparam>
    public readonly struct PooledObjectReturner<T> : IDisposable, IEquatable<PooledObjectReturner<T>>
        where T : class
    {
        private readonly ObjectPool<T>? _objectPool;
        private readonly T? _obj;
        private readonly int _token;
        private readonly PooledObjectReturnerSource? _source;

        internal PooledObjectReturner(ObjectPool<T> objectPool, T obj)
        {
            _objectPool = objectPool;
            _obj = obj;
            _source = PooledObjectReturnerSource.Allocate();
            _token = _source.Token;
        }

        /// <summary>
        /// Returns the pooled object to the pool.
        /// </summary>
        public void Dispose()
        {
            if (_objectPool == null
                || _obj is null
                || _source == null
                || _source.IsDisposed(_token))
            {
                return;
            }

            _objectPool.Return(_obj);
            _source.Dispose(_token);
        }

        bool IEquatable<PooledObjectReturner<T>>.Equals(PooledObjectReturner<T> other)
        {
            return Equals(in other);
        }

        public bool Equals(in PooledObjectReturner<T> other)
        {
            return (_source, _token) == (other._source, other._token);
        }

        public override bool Equals(object? obj)
        {
            return obj is PooledObjectReturner<T> pooledObjectReturner
                && Equals(in pooledObjectReturner);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_source, _token);
        }

        public static bool operator ==(in PooledObjectReturner<T> left, in PooledObjectReturner<T> right)
        {
            return left.Equals(in right);
        }

        public static bool operator !=(in PooledObjectReturner<T> left, in PooledObjectReturner<T> right)
        {
            return !left.Equals(in right);
        }
    }
}
