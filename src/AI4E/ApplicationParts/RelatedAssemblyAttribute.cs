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

/* Based on
* --------------------------------------------------------------------------------------------------------------------
* Asp.Net Core MVC
* Copyright (c) .NET Foundation. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use
* these files except in compliance with the License. You may obtain a copy of the
* License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed
* under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
* CONDITIONS OF ANY KIND, either express or implied. See the License for the
* specific language governing permissions and limitations under the License.
* --------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AI4E.ApplicationParts
{
    /// <summary>
    /// Specifies a assembly to load as part of MVC's assembly discovery mechanism.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class RelatedAssemblyAttribute : Attribute
    {
        private static readonly Func<string, Assembly> AssemblyLoadFileDelegate = Assembly.LoadFile;

        /// <summary>
        /// Initializes a new instance of <see cref="RelatedAssemblyAttribute"/>.
        /// </summary>
        /// <param name="assemblyFileName">The file name, without extension, of the related assembly.</param>
        public RelatedAssemblyAttribute(string assemblyFileName)
        {
            if (string.IsNullOrEmpty(assemblyFileName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(assemblyFileName));
            }

            AssemblyFileName = assemblyFileName;
        }

        /// <summary>
        /// Gets the assembly file name without extension.
        /// </summary>
        public string AssemblyFileName { get; }

        /// <summary>
        /// Gets <see cref="Assembly"/> instances specified by <see cref="RelatedAssemblyAttribute"/>.
        /// </summary>
        /// <param name="assembly">The assembly containing <see cref="RelatedAssemblyAttribute"/> instances.</param>
        /// <param name="throwOnError">Determines if the method throws if a related assembly could not be located.</param>
        /// <returns>Related <see cref="Assembly"/> instances.</returns>
        public static IReadOnlyList<Assembly> GetRelatedAssemblies(Assembly assembly, bool throwOnError)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            return GetRelatedAssemblies(assembly, throwOnError, File.Exists, AssemblyLoadFileDelegate);
        }

        internal static IReadOnlyList<Assembly> GetRelatedAssemblies(
            Assembly assembly,
            bool throwOnError,
            Func<string, bool> fileExists,
            Func<string, Assembly> loadFile)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            // MVC will specifically look for related parts in the same physical directory as the assembly.
            // No-op if the assembly does not have a location.
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.CodeBase))
            {
                return Array.Empty<Assembly>();
            }

            var attributes = assembly.GetCustomAttributes<RelatedAssemblyAttribute>().ToArray();
            if (attributes.Length == 0)
            {
                return Array.Empty<Assembly>();
            }

            var assemblyName = assembly.GetName().Name;
            var assemblyLocation = GetAssemblyLocation(assembly);
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

            var relatedAssemblies = new List<Assembly>();
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (string.Equals(assemblyName, attribute.AssemblyFileName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        string.Format("{0} specified on {1} cannot be self referential.", nameof(RelatedAssemblyAttribute), assemblyName));
                }

                var relatedAssemblyLocation = Path.Combine(assemblyDirectory, attribute.AssemblyFileName + ".dll");
                if (!fileExists(relatedAssemblyLocation))
                {
                    if (throwOnError)
                    {
                        throw new FileNotFoundException(
                            string.Format("Related assembly '{0}' specified by assembly '{1}' could not be found in the directory {2}. Related assemblies must be co-located with the specifying assemblies.",
                                          attribute.AssemblyFileName, assemblyName, assemblyDirectory),
                            relatedAssemblyLocation);
                    }
                    else
                    {
                        continue;
                    }
                }

                var relatedAssembly = loadFile(relatedAssemblyLocation);
                relatedAssemblies.Add(relatedAssembly);
            }

            return relatedAssemblies;
        }

        internal static string GetAssemblyLocation(Assembly assembly)
        {
            if (Uri.TryCreate(assembly.CodeBase, UriKind.Absolute, out var result) && result.IsFile)
            {
                return result.LocalPath;
            }

            return assembly.Location;
        }
    }
}
