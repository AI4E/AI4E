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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace AI4E.Utils.ApplicationParts.Test
{
    public class AssemblyPartTest
    {
        [Fact]
        public void AssemblyPart_Name_ReturnsAssemblyName()
        {
            // Arrange
            var part = new AssemblyPart(typeof(AssemblyPartTest).GetTypeInfo().Assembly);

            // Act
            var name = part.Name;

            // Assert
            Assert.Equal("AI4E.Utils.ApplicationParts.Test", name);
        }

        [Fact]
        public void AssemblyPart_Types_ReturnsDefinedTypes()
        {
            // Arrange
            var assembly = typeof(AssemblyPartTest).GetTypeInfo().Assembly;
            var part = new AssemblyPart(assembly);

            // Act
            var types = part.Types;

            // Assert
            Assert.Equal(assembly.DefinedTypes, types);
            Assert.NotSame(assembly.DefinedTypes, types);
        }

        [Fact]
        public void AssemblyPart_Assembly_ReturnsAssembly()
        {
            // Arrange
            var assembly = typeof(AssemblyPartTest).GetTypeInfo().Assembly;
            var part = new AssemblyPart(assembly);

            // Act & Assert
            Assert.Equal(part.Assembly, assembly);
        }

        // [Fact] // See: https://github.com/AI4E/AI4E/issues/277
        public void GetReferencePaths_ReturnsReferencesFromDependencyContext_IfPreserveCompilationContextIsSet()
        {
            // Arrange
            var assembly = GetType().GetTypeInfo().Assembly;
            var part = new AssemblyPart(assembly);

            // Act
            var references = part.GetReferencePaths().ToList();

            // Assert
            Assert.Contains(assembly.Location, references);
            Assert.Contains(
                typeof(AssemblyPart).GetTypeInfo().Assembly.GetName().Name,
                references.Select(Path.GetFileNameWithoutExtension));
        }

        [Fact]
        public void GetReferencePaths_ReturnsAssemblyLocation_IfPreserveCompilationContextIsNotSet()
        {
            // Arrange
            // src projects do not have preserveCompilationContext specified.
            var assembly = typeof(AssemblyPart).GetTypeInfo().Assembly;
            var part = new AssemblyPart(assembly);

            // Act
            var references = part.GetReferencePaths().ToList();

            // Assert
            var actual = Assert.Single(references);
            Assert.Equal(assembly.Location, actual);
        }

        [Fact]
        public void GetReferencePaths_ReturnsEmptySequenceForDynamicAssembly()
        {
            // Arrange
            var name = new AssemblyName($"DynamicAssembly-{Guid.NewGuid()}");
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name,
                AssemblyBuilderAccess.RunAndCollect);

            var part = new AssemblyPart(assembly);

            // Act
            var references = part.GetReferencePaths().ToList();

            // Assert
            Assert.Empty(references);
        }
    }
}
