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
* Asp.Net Blazor
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

namespace AI4E.Blazor.Module.Server
{
    internal class BlazorConfig
    {
        public string SourceMSBuildPath { get; }
        public string SourceOutputAssemblyPath { get; }
        public string WebRootPath { get; }
        public string DistPath => Path.Combine(Path.GetDirectoryName(SourceOutputAssemblyPath), "dist");
        public bool EnableAutoRebuilding { get; }
        public bool EnableDebugging { get; }

        public static BlazorConfig Read(string assemblyPath)
        {
            return new BlazorConfig(assemblyPath);
        }

        private BlazorConfig(string assemblyPath)
        {
            // TODO: Instead of assuming the lines are in a specific order, either JSON-encode
            // the whole thing, or at least give the lines key prefixes (e.g., "reload:<someuri>")
            // so we're not dependent on order and all lines being present.

            var configFilePath = Path.ChangeExtension(assemblyPath, ".blazor.config");
            var configLines = File.ReadLines(configFilePath).ToList();
            SourceMSBuildPath = configLines[0];

            if (SourceMSBuildPath == ".")
            {
                SourceMSBuildPath = assemblyPath;
            }

            var sourceMsBuildDir = Path.GetDirectoryName(SourceMSBuildPath);
            SourceOutputAssemblyPath = Path.Combine(sourceMsBuildDir, configLines[1]);

            var webRootPath = Path.Combine(sourceMsBuildDir, "wwwroot");
            if (Directory.Exists(webRootPath))
            {
                WebRootPath = webRootPath;
            }

            EnableAutoRebuilding = configLines.Contains("autorebuild:true", StringComparer.Ordinal);
            EnableDebugging = configLines.Contains("debug:true", StringComparer.Ordinal);
        }
    }
}
