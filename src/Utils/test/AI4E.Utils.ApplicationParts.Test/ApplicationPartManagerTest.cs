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
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AI4E.Utils.ApplicationParts.Test
{
    public class ApplicationPartManagerTest
    {
        [Fact]
        public void PopulateFeature_InvokesAllProvidersSequentially_ForAGivenFeature()
        {
            // Arrange
            var manager = new ApplicationPartManager();
            manager.ApplicationParts.Add(new ControllersPart("ControllersPartA"));
            manager.ApplicationParts.Add(new ViewComponentsPart("ViewComponentsPartB"));
            manager.ApplicationParts.Add(new ControllersPart("ControllersPartC"));
            manager.FeatureProviders.Add(
                new ControllersFeatureProvider((f, v) => f.Values.Add($"ControllersFeatureProvider1{v}")));
            manager.FeatureProviders.Add(
                new ControllersFeatureProvider((f, v) => f.Values.Add($"ControllersFeatureProvider2{v}")));

            var feature = new ControllersFeature();
            var expectedResults = new[]
            {
                "ControllersFeatureProvider1ControllersPartA",
                "ControllersFeatureProvider1ControllersPartC",
                "ControllersFeatureProvider2ControllersPartA",
                "ControllersFeatureProvider2ControllersPartC"
            };

            // Act
            manager.PopulateFeature(feature);

            // Assert
            Assert.Equal(expectedResults, feature.Values.ToArray());
        }

        [Fact]
        public void PopulateFeature_InvokesOnlyProviders_ForAGivenFeature()
        {
            // Arrange
            var manager = new ApplicationPartManager();
            manager.ApplicationParts.Add(new ControllersPart("ControllersPart"));
            manager.FeatureProviders.Add(
                new ControllersFeatureProvider((f, v) => f.Values.Add($"ControllersFeatureProvider{v}")));
            manager.FeatureProviders.Add(
                new NotControllersedFeatureProvider((f, v) => f.Values.Add($"ViewComponentsFeatureProvider{v}")));

            var feature = new ControllersFeature();
            var expectedResults = new[] { "ControllersFeatureProviderControllersPart" };

            // Act
            manager.PopulateFeature(feature);

            // Assert
            Assert.Equal(expectedResults, feature.Values.ToArray());
        }

        [Fact]
        public void PopulateFeature_SkipProviders_ForOtherFeatures()
        {
            // Arrange
            var manager = new ApplicationPartManager();
            manager.ApplicationParts.Add(new ViewComponentsPart("ViewComponentsPart"));
            manager.FeatureProviders.Add(
                new ControllersFeatureProvider((f, v) => f.Values.Add($"ControllersFeatureProvider{v}")));

            var feature = new ControllersFeature();

            // Act
            manager.PopulateFeature(feature);

            // Assert
            Assert.Empty(feature.Values.ToArray());
        }

        private class ControllersPart : ApplicationPart
        {
            public ControllersPart(string value)
            {
                Value = value;
            }

            public override string Name => "Test";

            public string Value { get; }
        }

        private class ViewComponentsPart : ApplicationPart
        {
            public ViewComponentsPart(string value)
            {
                Value = value;
            }

            public override string Name => "Other";

            public string Value { get; }
        }

        private class ControllersFeature
        {
            public IList<string> Values { get; } = new List<string>();
        }

        private class ViewComponentsFeature
        {
            public IList<string> Values { get; } = new List<string>();
        }

        private class NotControllersedFeatureProvider : IApplicationFeatureProvider<ViewComponentsFeature>
        {
            private readonly Action<ViewComponentsFeature, string> _operation;

            public NotControllersedFeatureProvider(Action<ViewComponentsFeature, string> operation)
            {
                _operation = operation;
            }

            public void PopulateFeature(IEnumerable<ApplicationPart> parts, ViewComponentsFeature feature)
            {
                foreach (var part in parts.OfType<ViewComponentsPart>())
                {
                    _operation(feature, part.Value);
                }
            }
        }

        private class ControllersFeatureProvider : IApplicationFeatureProvider<ControllersFeature>
        {
            private readonly Action<ControllersFeature, string> _operation;

            public ControllersFeatureProvider(Action<ControllersFeature, string> operation)
            {
                _operation = operation;
            }

            public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllersFeature feature)
            {
                foreach (var part in parts.OfType<ControllersPart>())
                {
                    _operation(feature, part.Value);
                }
            }
        }
    }
}
