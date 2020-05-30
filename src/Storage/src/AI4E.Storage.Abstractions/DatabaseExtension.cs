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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;

namespace AI4E.Storage
{
    /// <summary>
    /// Contains extension methods for the <see cref="IDatabase"/> type.
    /// </summary>
    public static partial class DatabaseExtension
    {
        /// <summary>
        /// Asynchronously retrieves a collection of all stored entries.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An async enumerable that enumerates all stored entries of type <typeparamref name="TEntry"/> 
        /// that match the specified predicate.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="database"/> is <c>null</c>.</exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        public static IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            this IDatabase database,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            return database.GetAsync<TEntry>(_ => true, cancellation);
        }

        /// <summary>
        /// Asynchronously retrieves a single entry that matches the specified predicate.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="predicate">The predicate that the entry must match.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TEntry}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains an entry that matches <paramref name="predicate"/> or
        /// <c>null</c> if no entry matched <paramref name="predicate"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="database"/> or <paramref name="predicate"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabase database,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            return database.GetAsync(predicate, cancellation).FirstOrDefaultAsync(cancellation)!;
        }

        /// <summary>
        /// Asynchronously retrieves a single entry that matches the specified entry type.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TEntry}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains an entry of type <typeparamref name="TEntry"/> or
        /// <c>null</c> if no entry of type <typeparamref name="TEntry"/> is found.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="database"/> is <c>null</c>.</exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabase database,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            return database.GetOneAsync<TEntry>(_ => true, cancellation);
        }

        /// <summary>
        /// Asynchronously tries to update the specified entry in the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="entry">The entry that shall be updated in the database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Boolean}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was updated 
        /// successfully.
        /// </returns>
        /// <remarks>
        /// The entry is updated successfully, if the database does contain an entry with the same id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="database"/> or <paramref name="entry"/> is <c>null</c>. 
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        public static ValueTask<bool> UpdateAsync<TEntry>(
            this IDatabase database,
            TEntry entry,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            return database.UpdateAsync(entry, _ => true, cancellation);
        }

        /// <summary>
        /// Asynchronously tries to remove the specified entry from the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="entry">The entry that shall be removed from the database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Boolean}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was removed 
        /// successfully.
        /// </returns>
        /// <remarks>
        /// The entry is removed successfully, if the database does contain an entry with the same id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="database"/> or <paramref name="entry"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        public static ValueTask<bool> RemoveAsync<TEntry>(
            this IDatabase database,
            TEntry entry,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            return database.RemoveAsync(entry, _ => true, cancellation);
        }

        /// <summary>
        /// Retrieves a single entry from the database or inserts the specified one if none is found.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="entry">The entry to insert if not entry with the same identifier is found.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TEntry}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains the entry with the same identifier then <paramref name="entry"/> 
        /// that was loaded from the database, or <paramref name="entry"/> after it was inserted into the database when 
        /// no existing entry matches.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="database"/> or <paramref name="entry"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        public static async ValueTask<TEntry> GetOrAddAsync<TEntry>(
            this IDatabase database,
            TEntry entry,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            var predicate = DataPropertyHelper.BuildPredicate(entry);

            if (predicate is null)
            {
                var success = await database.AddAsync(entry, cancellation).ConfigureAwait(false);
                Debug.Assert(success);
                return entry;
            }

            while (!await database.AddAsync(entry, cancellation).ConfigureAwait(false))
            {
                var result = await database.GetOneAsync(predicate, cancellation).ConfigureAwait(false);

                if (result != null)
                    return result;
            }

            return entry;
        }

        /// <summary>
        /// Adds the specified entry to the database or updated an existing item if present. 
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="entry">
        /// The entry that is inserted into the database or that an existing entry is updated with respectively.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="database"/> or <paramref name="entry"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        public static async ValueTask AddOrUpdateAsync<TEntry>(
            this IDatabase database,
            TEntry entry,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            while (!await database.AddAsync(entry, cancellation).ConfigureAwait(false))
            {
                var success = await database.UpdateAsync(entry, cancellation).ConfigureAwait(false);

                if (success)
                    return;
            }
        }

        /// <summary>
        /// Asynchronously replaces an entry with the specified one if the existing entry equals the specified 
        /// comparand.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="database">The database.</param>
        /// <param name="entry">The entry to insert on success.</param>
        /// <param name="comparand">The comparand entry.</param>
        /// <param name="entrySelector">An expression that identifies the entry to replace.</param>
        /// <param name="equalityComparer">An expression that specifies the equality of two entries.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Boolean}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the operation was successful.
        /// no existing entry matches.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="database"/>, <paramref name="entrySelector"/> 
        /// or <paramref name="equalityComparer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if any of <paramref name="entry"/> or <paramref name="comparand"/> is not <c>null</c> 
        /// and does not match <paramref name="entrySelector"/>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified entry selector or equality comparer.
        /// </exception>
        public static ValueTask<bool> CompareExchangeAsync<TEntry>(
           this IDatabase database,
           TEntry? entry,
           TEntry? comparand,
           Expression<Func<TEntry, bool>> entrySelector,
           Expression<Func<TEntry, TEntry, bool>> equalityComparer,
           CancellationToken cancellation = default) where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            if (entrySelector is null)
                throw new ArgumentNullException(nameof(entrySelector));

            if (equalityComparer is null)
                throw new ArgumentNullException(nameof(equalityComparer));

            Func<TEntry, bool>? compiledEntrySelector = null;

            if (entry != null && !CheckArgumentMatchesPredicate(entry, entrySelector, ref compiledEntrySelector))
            {
                throw new ArgumentException("The argument must match the specified entry selector.", nameof(entry));
            }

            if (comparand != null && !CheckArgumentMatchesPredicate(comparand, entrySelector, ref compiledEntrySelector))
            {
                throw new ArgumentException("The argument must match the specified entry selector.", nameof(comparand));
            }

            // This is a no-op actually. But we check whether comparand is up to date.
            // This includes the case, that both are null.
            if (entry == comparand)
            {
                return CheckComparandToBeUpToDateAsync(
                    database, comparand, entrySelector, equalityComparer, cancellation);
            }

            if (comparand is null)
            {
                Debug.Assert(entry != null);
                return database.AddAsync(entry!, cancellation);
            }

            var predicate = BuildPredicate(comparand, entrySelector, equalityComparer);

            if (entry is null)
            {
                return database.RemoveAsync(comparand, predicate, cancellation);
            }

            return database.UpdateAsync(entry, predicate, cancellation);
        }

        private static bool CheckArgumentMatchesPredicate<TEntry>(
            TEntry argument,
            Expression<Func<TEntry, bool>> entrySelector,
            ref Func<TEntry, bool>? compiledEntrySelector) where TEntry : class
        {
            if (entrySelector.Body.TryEvaluate(out var evaluationResult))
            {
                Debug.Assert(evaluationResult is bool);

                return (bool)evaluationResult;
            }

            compiledEntrySelector ??= entrySelector.Compile(preferInterpretation: true);
            return compiledEntrySelector(argument);
        }

        [Obsolete("This will be removed in a future version.")]
#pragma warning disable CS1591
        public static ValueTask<bool> CompareExchangeAsync<TEntry>(
#pragma warning restore CS1591
            this IDatabase database,
            TEntry? entry,
            TEntry? comparand,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer,
            CancellationToken cancellation = default)
           where TEntry : class
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            var predicate = DataPropertyHelper.BuildPredicate(comparand ?? entry ?? throw new ArgumentException());

            if (predicate is null)
            {
                return database.AddAsync(entry!, cancellation);
            }

            return CompareExchangeAsync(
                database,
                entry,
                comparand,
                predicate,
                equalityComparer,
                cancellation);
        }

        private static async ValueTask<bool> CheckComparandToBeUpToDateAsync<TEntry>(
            IDatabase database,
            TEntry? comparand,
            Expression<Func<TEntry, bool>> entrySelector,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer,
            CancellationToken cancellation)
            where TEntry : class
        {
            var result = await database.GetOneAsync(entrySelector, cancellation).ConfigureAwait(false);

            if (comparand is null)
            {
                return result is null;
            }

            if (result is null)
            {
                return false;
            }

            return equalityComparer.Compile(preferInterpretation: true).Invoke(comparand, result);
        }

        private static Expression<Func<TEntry, bool>> BuildPredicate<TEntry>(
            TEntry comparand,
            Expression<Func<TEntry, bool>> entrySelector,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer)
            where TEntry : class
        {
            Debug.Assert(comparand != null);
            Debug.Assert(entrySelector != null);
            Debug.Assert(equalityComparer != null);

            var comparandConstant = Expression.Constant(comparand, typeof(TEntry));
            var parameter = equalityComparer!.Parameters.First();
            var equalityComparerPass = ParameterExpressionReplacer.ReplaceParameter(
                equalityComparer.Body, equalityComparer.Parameters.Last(), comparandConstant);
            var entrySelectorPass = ParameterExpressionReplacer.ReplaceParameter(
                entrySelector!.Body, entrySelector.Parameters.First(), parameter);
            var body = Expression.AndAlso(entrySelectorPass, equalityComparerPass);

            return Expression.Lambda<Func<TEntry, bool>>(body, parameter);
        }
    }
}
