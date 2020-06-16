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

using System;
using System.Diagnostics;
using System.Linq;

namespace AI4E.Storage.Domain
{
    partial class UnitOfWork
    {
        private sealed class TrackedEntity : ITrackedEntity
        {
            #region Fields

            private readonly UnitOfWork _unitOfWork;
            private readonly object? _updatedEntity;

            #endregion

            #region C'tor

            private TrackedEntity(
                UnitOfWork unitOfWork,
                EntityTrackState trackState,
                IFoundEntityQueryResult entityLoadResult,
                ConcurrencyToken? concurrencyToken = null,
                long? revision = null,
                DomainEventCollection domainEvents = default,
                object? entity = null)
            {
                _unitOfWork = unitOfWork;
                TrackState = trackState;
                OriginalEntityLoadResult = entityLoadResult;

                UpdatedConcurrencyToken = concurrencyToken;
                UpdatedRevision = revision;
                DomainEvents = domainEvents;
                _updatedEntity = entity;
            }

            private TrackedEntity(
                UnitOfWork unitOfWork,
                EntityTrackState trackState,
                EntityIdentifier entityIdentifier,
                ConcurrencyToken? concurrencyToken = null,
                long? revision = null,
                DomainEventCollection domainEvents = default,
                object? entity = null)
            {
                _unitOfWork = unitOfWork;
                TrackState = trackState;
                OriginalEntityLoadResult = new NotFoundEntityQueryResult(entityIdentifier, loadedFromCache: true);

                UpdatedConcurrencyToken = concurrencyToken;
                UpdatedRevision = revision;
                DomainEvents = domainEvents;
                _updatedEntity = entity;
            }

            #endregion

            #region Factory methods

            public static TrackedEntity DeletedCacheEntry(
                UnitOfWork unitOfWork,
                IFoundEntityQueryResult entityLoadResult,
                ConcurrencyToken concurrencyToken,
                long revision,
                DomainEventCollection domainEvents)
            {
                return new TrackedEntity(unitOfWork, EntityTrackState.Deleted, entityLoadResult, concurrencyToken, revision, domainEvents);
            }

            public static TrackedEntity UnchangedCacheEntry(
                UnitOfWork unitOfWork,
                IFoundEntityQueryResult entityLoadResult,
                ConcurrencyToken? concurrencyToken = default,
                long? revision = default,
                DomainEventCollection domainEvents = default)
            {
                return new TrackedEntity(unitOfWork, EntityTrackState.Unchanged, entityLoadResult, concurrencyToken, revision, domainEvents);
            }

            public static TrackedEntity NonExistentCacheEntry(
                UnitOfWork unitOfWork,
                EntityIdentifier entityIdentifier,
                ConcurrencyToken? concurrencyToken = default,
                long? revision = default,
                DomainEventCollection domainEvents = default)
            {
                return new TrackedEntity(unitOfWork, EntityTrackState.NonExistent, entityIdentifier, concurrencyToken, revision, domainEvents);
            }

            public static TrackedEntity UpdatedCacheEntry(
                UnitOfWork unitOfWork,
                IFoundEntityQueryResult entityLoadResult,
                object entity,
                ConcurrencyToken concurrencyToken,
                long revision,
                DomainEventCollection domainEvents)
            {
                return new TrackedEntity(unitOfWork, EntityTrackState.Updated, entityLoadResult, concurrencyToken, revision, domainEvents, entity);
            }

            public static TrackedEntity CreatedCacheEntry(
                UnitOfWork unitOfWork,
                EntityIdentifier entityIdentifier,
                object entity,
                ConcurrencyToken concurrencyToken,
                long revision,
                DomainEventCollection domainEvents)
            {
                return new TrackedEntity(unitOfWork, EntityTrackState.Created, entityIdentifier, concurrencyToken, revision, domainEvents, entity);
            }

            public static TrackedEntity UntrackedCacheEntry(
                UnitOfWork unitOfWork,
                EntityIdentifier entityIdentifier,
                ConcurrencyToken? concurrencyToken = default,
                long? revision = default,
                DomainEventCollection domainEvents = default)
            {
                return new TrackedEntity(unitOfWork, EntityTrackState.Untracked, entityIdentifier, concurrencyToken, revision, domainEvents);
            }

            #endregion

            public EntityTrackState TrackState { get; }

            #region Originally loaded data

            public IEntityQueryResult OriginalEntityLoadResult { get; }

            private EntityIdentifier EntityIdentifier => OriginalEntityLoadResult.EntityIdentifier;

            #endregion

            #region Updated data

            public long? UpdatedRevision { get; }

            public ConcurrencyToken? UpdatedConcurrencyToken { get; }

            public DomainEventCollection DomainEvents { get; }

            #endregion

            #region Combined data

            public IEntityQueryResult EntityLoadResult
            {
                get
                {
                    if (TrackState == EntityTrackState.Deleted)
                        return new NotFoundEntityQueryResult(EntityIdentifier, loadedFromCache: true);

                    if (_updatedEntity != null)
                        return new FoundEntityQueryResult(EntityIdentifier, _updatedEntity, ConcurrencyToken, Revision, loadedFromCache: true);

                    return OriginalEntityLoadResult;
                }
            }

            public object? Entity
            {
                get
                {
                    if (TrackState == EntityTrackState.Deleted)
                        return null;

                    if (_updatedEntity != null)
                        return _updatedEntity;

                    return OriginalEntityLoadResult.GetEntity(throwOnFailure: false);
                }
            }

            object? ITrackedEntity.Entity => Entity;

            public ConcurrencyToken ConcurrencyToken
            {
                get
                {
                    var result = OriginalEntityLoadResult.ConcurrencyToken;

                    if (TrackState != EntityTrackState.Unchanged
                        && TrackState != EntityTrackState.NonExistent
                        && UpdatedConcurrencyToken != null)
                    {
                        result = UpdatedConcurrencyToken.Value;
                    }

                    Debug.Assert(!result.IsDefault);

                    return result;
                }
            }

            public long Revision
            {
                get
                {
                    if (TrackState != EntityTrackState.Unchanged
                        && TrackState != EntityTrackState.NonExistent
                        && UpdatedRevision != null)
                    {
                        return UpdatedRevision.Value;
                    }

                    return OriginalEntityLoadResult.Revision;
                }
            }

            #endregion

            #region Update/Delete

            public ITrackedEntity? Delete(DomainEventCollection domainEvents)
            {
                var updatedDomainEvents = DomainEvents.Concat(domainEvents);

                TrackedEntity trackedEntity;

                switch (TrackState)
                {
                    case EntityTrackState.Unchanged:
                    {
                        var concurrencyToken = UpdatedConcurrencyToken ?? _unitOfWork.CreateConcurrencyToken(EntityIdentifier);
                        var revision = UpdatedRevision ?? Revision + 1;

                        trackedEntity = DeletedCacheEntry(
                            _unitOfWork,
                            OriginalEntityLoadResult.AsSuccessLoadResult(),
                            concurrencyToken,
                            revision,
                            updatedDomainEvents);

                        break;
                    }
                    case EntityTrackState.Created:
                    {
                        trackedEntity = UntrackedCacheEntry(
                            _unitOfWork,
                            EntityIdentifier,
                            UpdatedConcurrencyToken,
                            UpdatedRevision,
                            updatedDomainEvents);

                        break;
                    }
                    case EntityTrackState.Updated:
                    {
                        trackedEntity = DeletedCacheEntry(
                            _unitOfWork,
                            OriginalEntityLoadResult.AsSuccessLoadResult(),
                            UpdatedConcurrencyToken!.Value,
                            UpdatedRevision!.Value,
                            updatedDomainEvents);

                        break;
                    }
                    case EntityTrackState.Deleted:
                    {
                        if (!domainEvents.Any())
                        {
                            trackedEntity = this;
                        }
                        else
                        {
                            trackedEntity = DeletedCacheEntry(
                                _unitOfWork,
                                OriginalEntityLoadResult.AsSuccessLoadResult(),
                                UpdatedConcurrencyToken!.Value,
                                UpdatedRevision!.Value,
                                updatedDomainEvents);
                        }
                        break;
                    }
                    case EntityTrackState.NonExistent:
                    {
                        if (!domainEvents.Any())
                        {
                            trackedEntity = this;
                        }
                        else
                        {
                            trackedEntity = NonExistentCacheEntry(
                                _unitOfWork,
                                EntityIdentifier,
                                UpdatedConcurrencyToken,
                                UpdatedRevision,
                                updatedDomainEvents);
                        }
                        break;
                    }
                    case EntityTrackState.Untracked:
                    default:
                        throw new InvalidOperationException();
                }

                if (trackedEntity != this)
                {
                    _unitOfWork.Update(trackedEntity);
                }

                if (trackedEntity.TrackState == EntityTrackState.Untracked)
                {
                    return null;
                }

                return trackedEntity;
            }

            public ITrackedEntity CreateOrUpdate(object entity, DomainEventCollection domainEvents)
            {
                if (entity is null)
                    throw new ArgumentNullException(nameof(entity));

                var updatedDomainEvents = DomainEvents.Concat(domainEvents);

                TrackedEntity trackedEntity;

                switch (TrackState)
                {
                    case EntityTrackState.NonExistent:
                    {
                        var concurrencyToken = UpdatedConcurrencyToken ?? _unitOfWork.CreateConcurrencyToken(EntityIdentifier);
                        var revision = UpdatedRevision ?? Revision + 1;

                        trackedEntity = CreatedCacheEntry(
                            _unitOfWork,
                            EntityIdentifier,
                            entity,
                            concurrencyToken,
                            revision,
                            updatedDomainEvents);

                        break;
                    }
                    case EntityTrackState.Unchanged:
                    {
                        var concurrencyToken = UpdatedConcurrencyToken ?? _unitOfWork.CreateConcurrencyToken(EntityIdentifier);
                        var revision = UpdatedRevision ?? Revision + 1;

                        trackedEntity = UpdatedCacheEntry(
                            _unitOfWork,
                            OriginalEntityLoadResult.AsSuccessLoadResult(),
                            entity,
                            concurrencyToken,
                            revision,
                            updatedDomainEvents);

                        break;
                    }
                    case EntityTrackState.Deleted:
                    {
                        trackedEntity = UpdatedCacheEntry(
                            _unitOfWork,
                            OriginalEntityLoadResult.AsSuccessLoadResult(),
                            entity,
                            UpdatedConcurrencyToken!.Value,
                            UpdatedRevision!.Value,
                            updatedDomainEvents);

                        break;
                    }
                    case EntityTrackState.Updated:
                    {
                        if (!domainEvents.Any())
                        {
                            trackedEntity = this;
                        }
                        else
                        {
                            trackedEntity = UpdatedCacheEntry(
                                _unitOfWork,
                                OriginalEntityLoadResult.AsSuccessLoadResult(),
                                Entity!,
                                UpdatedConcurrencyToken!.Value,
                                UpdatedRevision!.Value,
                                updatedDomainEvents);
                        }
                        break;
                    }
                    case EntityTrackState.Created:
                    {
                        if (!domainEvents.Any())
                        {
                            trackedEntity = this;
                        }
                        else
                        {
                            trackedEntity = CreatedCacheEntry(
                                _unitOfWork,
                                EntityIdentifier,
                                Entity!,
                                UpdatedConcurrencyToken!.Value,
                                UpdatedRevision!.Value,
                                updatedDomainEvents);
                        }
                        break;
                    }
                    case EntityTrackState.Untracked:
                    default:
                        throw new InvalidOperationException();
                }

                if (trackedEntity != this)
                {
                    _unitOfWork.Update(trackedEntity);
                }

                return trackedEntity;
            }

            #endregion
        }
    }
}
