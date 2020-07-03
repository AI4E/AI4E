/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityLoadResult"/>
    public abstract class EntityLoadResult : IEntityLoadResult
    {
        private protected EntityLoadResult(EntityIdentifier entityIdentifier)
        {
            EntityIdentifier = entityIdentifier;
        }

        /// <inheritdoc />
        public EntityIdentifier EntityIdentifier { get; }

        /// <inheritdoc />
        public virtual ConcurrencyToken ConcurrencyToken => default;

        /// <inheritdoc />
        public virtual long Revision => 0L;

        /// <inheritdoc />
        public abstract string Reason { get; }

        #region Features

        /// <inheritdoc cref="IEntityLoadResult.IsFound(out IFoundEntityQueryResult?)" />
        public virtual bool IsFound(
            [NotNullWhen(true)] out FoundEntityQueryResult? foundEntityQueryResult)
        {
            foundEntityQueryResult = null;
            return false;
        }

        bool IEntityLoadResult.IsFound([NotNullWhen(true)] out IFoundEntityQueryResult? foundEntityQueryResult)
        {
            foundEntityQueryResult = null;
            return IsFound(
                out Unsafe.As<IFoundEntityQueryResult, FoundEntityQueryResult>(ref foundEntityQueryResult!)!);
        }

        /// <inheritdoc cref="IEntityLoadResult.IsVerificationFailed(out IEntityVerificationResult?)" />
        public virtual bool IsVerificationFailed(
            [NotNullWhen(true)] out EntityVerificationResult? verificationEntityResult)
        {
            verificationEntityResult = null;
            return false;
        }

        bool IEntityLoadResult.IsVerificationFailed(
            [NotNullWhen(true)] out IEntityVerificationResult? verificationEntityResult)
        {
            verificationEntityResult = null;
            return IsVerificationFailed(
                out Unsafe.As<IEntityVerificationResult, EntityVerificationResult>(ref verificationEntityResult!)!);
        }

        /// <inheritdoc cref="IEntityLoadResult.IsScopeable{TQueryResult}(out IScopeableEntityQueryResult{TQueryResult}?)" />
        public virtual bool IsScopeable<TQueryResult>(
            [NotNullWhen(true)] out IScopeableEntityQueryResult<TQueryResult>? scopeableEntityQueryResult)
            where TQueryResult : class, IEntityQueryResult
        {
            scopeableEntityQueryResult = null;
            return false;
        }

        /// <inheritdoc cref="IEntityLoadResult.IsTrackable{TLoadResult}(out ITrackableEntityLoadResult{TLoadResult}?)" />
        public virtual bool IsTrackable<TLoadResult>(
            [NotNullWhen(true)] out ITrackableEntityLoadResult<TLoadResult>? trackableEntityLoadResult)
            where TLoadResult : class, IEntityLoadResult
        {
            trackableEntityLoadResult = null;
            return false;
        }

        #endregion
    }
}