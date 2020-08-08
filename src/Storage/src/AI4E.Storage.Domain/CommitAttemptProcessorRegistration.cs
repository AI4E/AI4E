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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    public sealed class CommitAttemptProcessorRegistration : ICommitAttemptProcessorRegistration
    {
        private readonly Func<IServiceProvider, ICommitAttemptProcessor> _factory;

        public CommitAttemptProcessorRegistration(Type commitAttemptProcessorType)
        {
            if (commitAttemptProcessorType is null)
                throw new ArgumentNullException(nameof(commitAttemptProcessorType));

            CommitAttemptProcessorType = commitAttemptProcessorType;
            _factory = serviceProvider => (ICommitAttemptProcessor)ActivatorUtilities.CreateInstance(
                serviceProvider, commitAttemptProcessorType);
        }

        public CommitAttemptProcessorRegistration(
            Type commitAttemptProcessorType,
            Func<IServiceProvider, ICommitAttemptProcessor> factory)
        {
            if (commitAttemptProcessorType is null)
                throw new ArgumentNullException(nameof(commitAttemptProcessorType));

            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            CommitAttemptProcessorType = commitAttemptProcessorType;
            _factory = factory;
        }

        public CommitAttemptProcessorRegistration(ICommitAttemptProcessor commitAttemptProcessor)
        {
            if (commitAttemptProcessor is null)
                throw new ArgumentNullException(nameof(commitAttemptProcessor));

            CommitAttemptProcessorType = commitAttemptProcessor.GetType();
            _factory = _ => commitAttemptProcessor;
        }

        public ICommitAttemptProcessor CreateCommitAttemptProcessor(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return _factory(serviceProvider);
        }

        public Type CommitAttemptProcessorType { get; }

        public static CommitAttemptProcessorRegistration Create<TProcessor>()
            where TProcessor : class, ICommitAttemptProcessor
        {
            return new CommitAttemptProcessorRegistration(typeof(TProcessor));
        }

        public static CommitAttemptProcessorRegistration Create<TProcessor>(Func<IServiceProvider, TProcessor> factory)
            where TProcessor : class, ICommitAttemptProcessor
        {
            return new CommitAttemptProcessorRegistration(typeof(TProcessor), factory);
        }
    }
}
