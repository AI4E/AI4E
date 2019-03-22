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
    [MessageHandler]
    public abstract class MessageHandler
    {
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; internal set; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; internal set; }

        [NoMessageHandler]
        public virtual FailureDispatchResult Failure()
        {
            return new FailureDispatchResult();
        }

        [NoMessageHandler]
        public virtual FailureDispatchResult Failure(string message)
        {
            return new FailureDispatchResult(message);
        }

        [NoMessageHandler]
        public virtual FailureDispatchResult Failure(string message, IDispatchResult underlyingResult) // TODO
        {
            return new FailureDispatchResult(message);
        }

        [NoMessageHandler]
        public virtual FailureDispatchResult Failure(Exception exc)
        {
            return new FailureDispatchResult(exc);
        }

        [NoMessageHandler]
        public virtual SuccessDispatchResult Success()
        {
            return new SuccessDispatchResult();
        }

        [NoMessageHandler]
        public virtual SuccessDispatchResult Success(string message)
        {
            return new SuccessDispatchResult(message);
        }

        [NoMessageHandler]
        public virtual SuccessDispatchResult<TResult> Success<TResult>(TResult result)
        {
            return new SuccessDispatchResult<TResult>(result);
        }

        [NoMessageHandler]
        public virtual SuccessDispatchResult<TResult> Success<TResult>(TResult result, string message)
        {
            return new SuccessDispatchResult<TResult>(result, message);
        }

        [NoMessageHandler]
        public virtual ConcurrencyIssueDispatchResult ConcurrencyIssue()
        {
            return new ConcurrencyIssueDispatchResult();
        }

        [NoMessageHandler]
        public virtual EntityNotFoundDispatchResult EntityNotFound(Type entityType, string id)
        {
            return new EntityNotFoundDispatchResult(entityType, id);
        }

        [NoMessageHandler]
        public virtual EntityNotFoundDispatchResult EntityNotFound<TEntity>(string id)
        {
            return new EntityNotFoundDispatchResult(typeof(TEntity), id);
        }

        [NoMessageHandler]
        public virtual EntityNotFoundDispatchResult EntityNotFound<TEntity>(object id)
        {
            return new EntityNotFoundDispatchResult(typeof(TEntity), id.ToString());
        }

        [NoMessageHandler]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent(Type entityType, string id)
        {
            return new EntityAlreadyPresentDispatchResult(entityType, id);
        }

        [NoMessageHandler]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent<TEntity>(string id)
        {
            return new EntityAlreadyPresentDispatchResult(typeof(TEntity), id);
        }

        [NoMessageHandler]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent<TEntity>(object id)
        {
            return new EntityAlreadyPresentDispatchResult(typeof(TEntity), id.ToString());
        }

        [NoMessageHandler]
        public virtual NotAuthenticatedDispatchResult NotAuthenticated()
        {
            return new NotAuthenticatedDispatchResult();
        }

        [NoMessageHandler]
        public virtual NotAuthorizedDispatchResult NotAuthorized()
        {
            return new NotAuthorizedDispatchResult();
        }

        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure()
        {
            return new ValidationFailureDispatchResult();
        }

        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure(IEnumerable<ValidationResult> validationResults)
        {
            return new ValidationFailureDispatchResult(validationResults);
        }

        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure(params ValidationResult[] validationResults)
        {
            return new ValidationFailureDispatchResult(validationResults);
        }

        [NoMessageHandler]
        public virtual ValidationFailureDispatchResult ValidationFailure(string member, string message)
        {
            return new ValidationFailureDispatchResult(new[] { new ValidationResult(member, message) });
        }

        [NoMessageHandler]
        public virtual NotFoundDispatchResult NotFound()
        {
            return new NotFoundDispatchResult();
        }

        [NoMessageHandler]
        public virtual NotFoundDispatchResult NotFound(string message)
        {
            return new NotFoundDispatchResult(message);
        }

        [NoMessageHandler]
        public virtual DispatchFailureDispatchResult DispatchFailure()
        {
            var messageType = Context.DispatchData.MessageType;
            return new DispatchFailureDispatchResult(messageType);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerAttribute : Attribute
    {
        public MessageHandlerAttribute() { }

        public MessageHandlerAttribute(Type messageType)
        {
            MessageType = messageType;
        }

        public Type MessageType { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class NoMessageHandlerAttribute : Attribute { }

    /// <summary>
    /// An attribute that identifies a message handler's message-dispatcher property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MessageDispatcherAttribute : Attribute { }
}
