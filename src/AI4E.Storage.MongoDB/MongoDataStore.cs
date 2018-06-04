/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static AI4E.Storage.MongoDB.MongoWriteHelper;

namespace AI4E.Storage.MongoDB
{
    [Obsolete]
    public sealed class MongoDataStore : IDataStore
    {
        private readonly ConcurrentDictionary<Type, object> _collections = new ConcurrentDictionary<Type, object>();
        private readonly IMongoDatabase _database;

        public MongoDataStore(IMongoDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        private IMongoCollection<TData> GetCollection<TData>()
        {
            return (IMongoCollection<TData>)_collections.GetOrAdd(typeof(TData), t => _database.GetCollection<TData>("data-store." + typeof(TData).FullName));
        }

        public async Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            var collection = GetCollection<TData>();

            var updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(BuildPredicate(data),
                                                                                                    data,
                                                                                                    options: new UpdateOptions { IsUpsert = true },
                                                                                                    cancellationToken: cancellation));

            if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 && updateResult.UpsertedId == null)
            {
                throw new StorageException();
            }
        }

        public async Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            var collection = GetCollection<TData>();

            var deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(BuildPredicate(data), cancellationToken: cancellation));

            if (!deleteResult.IsAcknowledged || deleteResult.DeletedCount == 0)
            {
                throw new StorageException();
            }
        }

        public async Task<IEnumerable<TResult>> QueryAsync<TData, TResult>(Func<IQueryable<TData>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default)
            where TData : class
        {
            return await ((IMongoQueryable<TResult>)queryShaper(GetCollection<TData>().AsQueryable())).ToListAsync(cancellation);
        }

        private Expression<Func<TData, bool>> BuildPredicate<TData>(TData comparand)
        {
            var idMember = BsonClassMap.LookupClassMap(typeof(TData))?.IdMemberMap?.MemberInfo;

            if (idMember == null)
            {
                throw new StorageException($"Unable to resolve primary key for type '{typeof(TData).FullName}'");
            }

            Type idType;
            Expression c1, c2;
            var param = Expression.Parameter(typeof(TData));

            if (idMember.MemberType == MemberTypes.Method)
            {
                c1 = Expression.Call(Expression.Constant(comparand), (MethodInfo)idMember);
                c2 = Expression.Call(param, (MethodInfo)idMember);
                idType = ((MethodInfo)idMember).ReturnType;
            }
            else
            {
                c1 = Expression.MakeMemberAccess(Expression.Constant(comparand), idMember);
                c2 = Expression.MakeMemberAccess(param, idMember);

                if (idMember.MemberType == MemberTypes.Field)
                {
                    idType = ((FieldInfo)idMember).FieldType;
                }
                else if (idMember.MemberType == MemberTypes.Property)
                {
                    idType = ((PropertyInfo)idMember).PropertyType;
                }
                else
                {
                    return null;
                }
            }

            var equalityOperator = idType.GetMethod("op_Equality", BindingFlags.Static); // TODO

            if (equalityOperator != null)
            {
                return Expression.Lambda<Func<TData, bool>>(Expression.Equal(c1, c2), param);
            }
            else if (idType.GetInterfaces().Any(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IEquatable<>) && p.GetGenericArguments()[0] == idType))
            {
                var equalsMethod = typeof(IEquatable<>).MakeGenericType(idType).GetMethod(nameof(Equals));

                return Expression.Lambda<Func<TData, bool>>(Expression.Call(c1, equalsMethod, c2), param);
            }
            else
            {
                var equalsMethod = typeof(object).GetMethod(nameof(Equals));

                return Expression.Lambda<Func<TData, bool>>(Expression.Call(c1, equalsMethod, c2), param);
            }
        }

        public Task Clear(CancellationToken cancellation = default)
        {
            throw new NotImplementedException(); // TODO
        }
    }
}
