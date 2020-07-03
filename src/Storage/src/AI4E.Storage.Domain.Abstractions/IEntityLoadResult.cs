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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents the result of an entity load operation.
    /// </summary>
    public interface IEntityLoadResult
    {
        /// <summary>
        /// Gets the <see cref="EntityIdentifier"/> of the entity that a load operation was performed for.
        /// </summary>
        EntityIdentifier EntityIdentifier { get; }

        /// <summary>
        /// Gets the concurrency-token of the loaded entity or a default value if not available.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be available only in case that the load operation was successful.
        /// </remarks>
        ConcurrencyToken ConcurrencyToken { get; } // TODO: Move this where it belongs to. (IFoundEntityQueryResult ??)

        /// <summary>
        /// Gets the revision of the loaded entity or a default value if not available.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be available only in case that the load operation was successful.
        /// </remarks>
        long Revision { get; }  // TODO: Move this where it belongs to. (IFoundEntityQueryResult ??)

        /// <summary>
        /// Gets the reason phrase that indicates the reason of the load-result state or a failure message.
        /// </summary>
        string Reason { get; }

        #region Features

        // Currently the set of features is fixed to the four features 
        // * found
        // * verification-failure, 
        // * scopeability, 
        // * trackability. 
        // This could be extended in the future via default interface implementations.
        // Additionally features also inherit from the feature-descriptor == IEntityLoadResult to allow building APIs
        // that enforces some feature to be present.
        // 
        // TODO: Consider alternative designs:
        // (1) We could use a single method 
        //     bool HasFeature<TFeature>([NotNullWhen(true)] out TFeature? feature) where TFeature : class, IEntityLoadResult
        //     as a substitute for the multiple features as implemented currently.
        // (2) The features themselves could be made independent of the feature descriptor by not inheriting from it.
        //     This needs the feature descriptor to be generic on the feature, as we want to be able to build 
        //     compiler enforced APIs that guarantee a feature to be present.
        //     See: IEntityStorageEngine.QueryEntityAsync vs IEntityStorageEngine.QueryEntitiesAsync

        // TODO: Specify what found means.. When the verification failed, does this mean that the entity was found?

        /// <summary>
        /// Checks whether the current entity load-result represents a found entity query-result.
        /// </summary>
        /// <param name="foundEntityQueryResult">
        /// Contains the <see cref="IFoundEntityQueryResult"/> if the current entity load-result represents a found 
        /// entity query-result.
        /// </param>
        /// <returns>
        /// True if the current entity load-result represents a found entity query-result, false otherwise.
        /// </returns>
        bool IsFound([NotNullWhen(true)] out IFoundEntityQueryResult? foundEntityQueryResult);

        /// <summary>
        /// Checks whether the current entity load-result represents a verification failure entity query-result.
        /// </summary>
        /// <param name="verificationEntityResult">
        /// Contains the <see cref="IEntityVerificationResult"/> if the current entity load-result represents a 
        /// verification failure  entity query-result.
        /// </param>
        /// <returns>
        /// True if the current entity load-result represents a verification failure entity query-result, 
        /// false otherwise.
        /// </returns>
        bool IsVerificationFailed([NotNullWhen(true)] out IEntityVerificationResult? verificationEntityResult);

        /// <summary>
        /// Checks whether the current entity load-result is scopeable with respect to the specified query-result type.
        /// </summary>
        /// <typeparam name="TQueryResult">The type of entity query-result.</typeparam>
        /// <param name="scopeableEntityQueryResult">
        /// Contains the <see cref="IScopeableEntityQueryResult{TQueryResult}"/> if the current entity load-result 
        /// is scopeable.
        /// </param>
        /// <returns>
        /// True if the current entity load-result is scopeable, false otherwise.
        /// </returns>
        bool IsScopeable<TQueryResult>(
            [NotNullWhen(true)] out IScopeableEntityQueryResult<TQueryResult>? scopeableEntityQueryResult)
            where TQueryResult : class, IEntityQueryResult;

        /// <summary>
        /// Checks whether the current entity load-result is a track-able entity load-result with respect to the
        /// specified entity-load result type.
        /// </summary>
        /// <typeparam name="TLoadResult">The type of tracked entity load-result.</typeparam>
        /// <param name="trackableEntityLoadResult">
        /// Contains the <see cref="ITrackableEntityLoadResult{TLoadResult}"/> if the current entity load-result 
        /// is track-able.
        /// </param>
        /// <returns>
        /// True if the current entity load-result is track-able, 
        /// false otherwise.
        /// </returns>
        bool IsTrackable<TLoadResult>(
           [NotNullWhen(true)] out ITrackableEntityLoadResult<TLoadResult>? trackableEntityLoadResult)
            where TLoadResult : class, IEntityLoadResult;

        #endregion
    }
}