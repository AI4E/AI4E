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
using System.Threading.Tasks;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB
{
    internal static class MongoExceptionHelper
    {
        public static async Task TryOperation(Func<Task> operation)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (MongoWriteException exc) when (exc.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                throw new DuplicateKeyException();
            }
            catch (MongoWriteException exc) when (exc.WriteError?.Category == ServerErrorCategory.ExecutionTimeout)
            {
                throw new StorageUnavailableException("The storage engine operation timed out.", exc);
            }
            catch (TimeoutException exc)
            {
                throw new StorageUnavailableException("The storage engine operation timed out.", exc);
            }
            catch (MongoConnectionException exc)
            {
                throw new StorageUnavailableException("The storage engine operation timed out.", exc);
            }
            catch (MongoException exc)
            {
                throw new StorageException("An unknown error occured.", exc);
            }
        }

        public static async Task<T> TryWriteOperation<T>(Func<Task<T>> operation)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (MongoWriteException exc) when (exc.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                throw new DuplicateKeyException();
            }
            catch (MongoWriteException exc) when (exc.WriteError?.Category == ServerErrorCategory.ExecutionTimeout)
            {
                throw new StorageUnavailableException("The storage engine operation timed out.", exc);
            }
            catch (TimeoutException exc)
            {
                throw new StorageUnavailableException("The storage engine operation timed out.", exc);
            }
            catch (MongoConnectionException exc)
            {
                throw new StorageUnavailableException("The storage engine operation timed out.", exc);
            }
            catch (MongoException exc)
            {
                throw new StorageException("An unknown error occured.", exc);
            }
        }
    }
}
