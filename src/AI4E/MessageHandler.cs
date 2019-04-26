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
using System.Collections.Generic;
using AI4E.DispatchResults;

namespace AI4E
{
    /// <summary>
    /// An abstract base class that can be used to implement message handlers.
    /// </summary>
    [MessageHandler]
    public abstract class MessageHandler
    {
        /// <summary>
        /// Gets the message handler context.
        /// </summary>
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; internal set; }

        /// <summary>
        /// Gets the message dispatcher that is used to dispatch messages.
        /// </summary>
        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; internal set; }

        /// <summary>
        /// Returns a failure dispatch result.
        /// </summary>
        /// <returns>A <see cref="FailureDispatchResult"/> that indicates a dispatch failure.</returns>
        [NoMessageHandler]
        public virtual FailureDispatchResult Failure()
        {
            return new FailureDispatchResult();
        }

        /// <summary>
        /// Returns a failure dispatch result with the specified message.
        /// </summary>
        /// <param name="message">The failure message.</param>
        /// <returns>A <see cref="FailureDispatchResult"/> that indicates a dispatch failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        [NoMessageHandler]
        public virtual FailureDispatchResult Failure(string message)
        {
            return new FailureDispatchResult(message);
        }

        /// <summary>
        /// Returns a failure dispatch result generated from the specified exception.
        /// </summary>
        /// <param name="exc">The exception that causes the dispatch failure.</param>
        /// <returns>A <see cref="FailureDispatchResult"/> that indicates a dispatch failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exc"/> is <c>null</c>.</exception>
        [NoMessageHandler]
        public virtual FailureDispatchResult Failure(Exception exc)
        {
            return new FailureDispatchResult(exc);
        }

        /// <summary>
        /// Returns a success dispatch result.
        /// </summary>
        /// <returns>A <see cref="SuccessDispatchResult"/> that indicates a dispatch success.</returns>
        [NoMessageHandler]
        public virtual SuccessDispatchResult Success()
        {
            return new SuccessDispatchResult();
        }

        /// <summary>
        /// Returns a success dispatch result with the specified message.
        /// </summary>
        /// <param name="message">The result message.</param>
        /// <returns>A <see cref="SuccessDispatchResult"/> that indicates a dispatch success.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        [NoMessageHandler]
        public virtual SuccessDispatchResult Success(string message)
        {
            return new SuccessDispatchResult(message);
        }

        /// <summary>
        /// Returns a success dispatch result with the specified result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>A <see cref="SuccessDispatchResult{TResult}"/> that indicates a dispatch success.</returns>
        [NoMessageHandler]
        public virtual SuccessDispatchResult<TResult> Success<TResult>(TResult result)
        {
            return new SuccessDispatchResult<TResult>(result);
        }

        /// <summary>
        /// Returns a success dispatch result with the specified result and message.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="message">The result message.</param>
        /// <returns>A <see cref="SuccessDispatchResult{TResult}"/> that indicates a dispatch success.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        [NoMessageHandler]
        public virtual SuccessDispatchResult<TResult> Success<TResult>(TResult result, string message)
        {
            return new SuccessDispatchResult<TResult>(result, message);
        }

        /// <summary>
        /// Returns a concurrency issue result.
        /// </summary>
        /// <returns>
        /// A <see cref="ConcurrencyIssueDispatchResult"/> that indicates
        /// a concurrency issue in the dispatch operation.
        /// </returns>
        [NoMessageHandler]
        public virtual ConcurrencyIssueDispatchResult ConcurrencyIssue()
        {
            return new ConcurrencyIssueDispatchResult();
        }

        /// <summary>
        /// Returns an entity-not-found result.
        /// </summary>
        /// <param name="entityType">The type of entity that cannot be found.</param>
        /// <param name="id">The id of the entity that cannot be found.</param>
        /// <returns>
        /// An <see cref="EntityNotFoundDispatchResult"/> that indicates that an entity cannot be found.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entityType"/> or <paramref name="id"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual EntityNotFoundDispatchResult EntityNotFound(Type entityType, string id)
        {
            return new EntityNotFoundDispatchResult(entityType, id);
        }

        /// <summary>
        /// Returns an entity-not-found result.
        /// </summary>
        /// <param name="id">The id of the entity that cannot be found.</param>
        /// <returns>
        /// An <see cref="EntityNotFoundDispatchResult"/> that indicates that an entity cannot be found.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="id"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual EntityNotFoundDispatchResult EntityNotFound<TEntity>(string id)
        {
            return new EntityNotFoundDispatchResult(typeof(TEntity), id);
        }

        /// <summary>
        /// Returns an entity-not-found result.
        /// </summary>
        /// <param name="id">The id of the entity that cannot be found.</param>
        /// <returns>
        /// An <see cref="EntityNotFoundDispatchResult"/> that indicates that an entity cannot be found.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="id"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual EntityNotFoundDispatchResult EntityNotFound<TEntity>(object id)
        {
            return new EntityNotFoundDispatchResult(typeof(TEntity), id.ToString());
        }

        /// <summary>
        /// Returns an entity-already-present result.
        /// </summary>
        /// <param name="entityType">The type of entity that an id conflict occured.</param>
        /// <param name="id">The conflicting entity id.</param>
        /// <returns>
        /// An <see cref="EntityAlreadyPresentDispatchResult"/> that indicates that an entity id
        /// conflict occured.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entityType"/> or <paramref name="id"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent(Type entityType, string id)
        {
            return new EntityAlreadyPresentDispatchResult(entityType, id);
        }

        /// <summary>
        /// Returns an entity-already-present result.
        /// </summary>
        /// <param name="id">The conflicting entity id.</param>
        /// <returns>
        /// An <see cref="EntityAlreadyPresentDispatchResult"/> that indicates that an entity id
        /// conflict occured.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="id"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent<TEntity>(string id)
        {
            return new EntityAlreadyPresentDispatchResult(typeof(TEntity), id);
        }

        /// <summary>
        /// Returns an entity-already-present result.
        /// </summary>
        /// <param name="id">The conflicting entity id.</param>
        /// <returns>
        /// An <see cref="EntityAlreadyPresentDispatchResult"/> that indicates that an entity id
        /// conflict occured.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="id"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent<TEntity>(object id)
        {
            return new EntityAlreadyPresentDispatchResult(typeof(TEntity), id.ToString());
        }

        /// <summary>
        /// Returns a not-authenticated result.
        /// </summary>
        /// <returns>
        /// A <see cref="NotAuthenticatedDispatchResult"/> that indicates that a user must authenticate.
        /// </returns>
        [NoMessageHandler]
        public virtual NotAuthenticatedDispatchResult NotAuthenticated()
        {
            return new NotAuthenticatedDispatchResult();
        }

        /// <summary>
        /// Returns a not-authorized result.
        /// </summary>
        /// <returns>
        /// A <see cref="NotAuthorizedDispatchResult"/> that indicates that a user must be authorized.
        /// </returns>
        [NoMessageHandler]
        public virtual NotAuthorizedDispatchResult NotAuthorized()
        {
            return new NotAuthorizedDispatchResult();
        }

        /// <summary>
        /// Returns a validation failure result.
        /// </summary>
        /// <returns>
        /// A <see cref="ValidationFailureDispatchResult"/> indicating that a validation failure occured.
        /// </returns>
        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure()
        {
            return new ValidationFailureDispatchResult();
        }

        /// <summary>
        /// Returns a validation failure result with the specified validation results.
        /// </summary>
        /// <param name="validationResults">A collection of <see cref="ValidationResult"/>s.</param>
        /// <returns>
        /// A <see cref="ValidationFailureDispatchResult"/> indicating that a validation failure occured.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="validationResults"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure(IEnumerable<ValidationResult> validationResults)
        {
            return new ValidationFailureDispatchResult(validationResults);
        }

        /// <summary>
        /// Returns a validation failure result with the specified validation results.
        /// </summary>
        /// <param name="validationResults">An array of <see cref="ValidationResult"/>s.</param>
        /// <returns>
        /// A <see cref="ValidationFailureDispatchResult"/> indicating that a validation failure occured.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="validationResults"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure(params ValidationResult[] validationResults)
        {
            return new ValidationFailureDispatchResult(validationResults);
        }

        /// <summary>
        /// Returns a validation failure result with the specified validation results.
        /// </summary>
        /// <param name="member">The name of the member that is invalid.</param>
        /// <param name="message">The validation message.</param>
        /// <returns>
        /// A <see cref="ValidationFailureDispatchResult"/> indicating that a validation failure occured.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="member"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure(string member, string message)
        {
            return new ValidationFailureDispatchResult(new[] { new ValidationResult(member, message) });
        }

        /// <summary>
        /// Returns a not-found result.
        /// </summary>
        /// <returns>
        /// A <see cref="NotFoundDispatchResult"/> indicating that a mandatory resource cannot be found.
        /// </returns>
        [NoMessageHandler]
        public virtual NotFoundDispatchResult NotFound()
        {
            return new NotFoundDispatchResult();
        }

        /// <summary>
        /// Returns a not-found result with the specified message.
        /// </summary>
        /// <param name="message">The result message.</param>
        /// <returns>
        /// A <see cref="NotFoundDispatchResult"/> indicating that a mandatory resource cannot be found.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        [NoMessageHandler]
        public virtual NotFoundDispatchResult NotFound(string message)
        {
            return new NotFoundDispatchResult(message);
        }

        /// <summary>
        /// Returns a dispatch-failure result.
        /// </summary>
        /// <returns>A <see cref="DispatchFailureDispatchResult"/> indicating that a message cannot be dispatched.</returns>
        [NoMessageHandler]
        public virtual DispatchFailureDispatchResult DispatchFailure()
        {
            var messageType = Context.DispatchData.MessageType;
            return new DispatchFailureDispatchResult(messageType);
        }
    }
}
