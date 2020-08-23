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
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using AI4E.Utils;

namespace AI4E.Messaging.MessageHandlers
{
    public class MessageHandlerResolver : IMessageHandlerResolver
    {
        private static readonly IAssemblyResolver _assemblyResolver = new HasReferenceAssemblyResolver(
            typeof(IMessageDispatcher).Assembly.GetName(),
            AssemblyNameComparer.BySimpleName);

        private static readonly Func<Type, bool> _isMessageHandler
            = new Func<Type, bool>(IsMessageHandler);

        public IEnumerable<Type> ResolveMessageHandlers(
            Assembly assembly,
            AssemblyLoadContext? loadContext)
        {
            // Default comparison is ok for this case.
            var resultsBuilder = ImmutableHashSet.CreateBuilder<Type>();

            foreach (var resolvedAssembly in _assemblyResolver.GetAssemblies(assembly, loadContext))
            {
                resultsBuilder.UnionWith(GetMessageHandlers(resolvedAssembly));
            }

            return resultsBuilder.ToImmutable();
        }

        public static IEnumerable<Type> GetMessageHandlers(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            return assembly.DefinedTypes.Where(_isMessageHandler);
        }

        public static bool IsMessageHandler(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return IsMessageHandler(type, allowAbstract: false);
        }

        private static bool IsMessageHandler(Type type, bool allowAbstract)
        {
            if (type.IsInterface || type.IsEnum)
                return false;

            if (!allowAbstract && type.IsAbstract)
                return false;

            if (type.ContainsGenericParameters)
                return false;

            if (type.IsDefined<NoMessageHandlerAttribute>(inherit: false))
                return false;

            if (type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) && type.IsPublic)
                return true;

            if (type.IsDefined<MessageHandlerAttribute>(inherit: false))
                return true;

            return type.BaseType != null && IsMessageHandler(type.BaseType, allowAbstract: true);
        }
    }
}
