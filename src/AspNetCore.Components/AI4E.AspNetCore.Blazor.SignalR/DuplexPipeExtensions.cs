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
 * BlazorSignalR (https://github.com/csnewman/BlazorSignalR)
 *
 * MIT License
 *
 * Copyright (c) 2018 csnewman
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    public static class DuplexPipeExtensions
    {
        public static Task StartAsync(this IDuplexPipe pipe, Uri uri, TransferFormat transferFormat)
        {
#pragma warning disable CA1062
            var method = pipe.GetType().GetMethod(
                nameof(StartAsync), new Type[] { typeof(Uri), typeof(TransferFormat), typeof(CancellationToken) });
#pragma warning restore CA1062
            Debug.Assert(method != null);
            var result = method!.Invoke(pipe, new object[] { uri, transferFormat, default(CancellationToken) }) as Task;
            Debug.Assert(result != null);
            return result!;
        }

        public static Task StopAsync(this IDuplexPipe pipe)
        {
#pragma warning disable CA1062
            var method = pipe.GetType().GetMethod(nameof(StopAsync), Array.Empty<Type>());
#pragma warning restore CA1062
            Debug.Assert(method != null);
            var result = method!.Invoke(pipe, Array.Empty<object>()) as Task;
            Debug.Assert(result != null);
            return result!;
        }
    }
}
