///* License
// * --------------------------------------------------------------------------------------------------------------------
// * This file is part of the AI4E distribution.
// *   (https://github.com/AI4E/AI4E)
// * Copyright (c) 2018 Andreas Truetschel and contributors.
// * 
// * AI4E is free software: you can redistribute it and/or modify  
// * it under the terms of the GNU Lesser General Public License as   
// * published by the Free Software Foundation, version 3.
// *
// * AI4E is distributed in the hope that it will be useful, but 
// * WITHOUT ANY WARRANTY; without even the implied warranty of 
// * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
// * Lesser General Public License for more details.
// *
// * You should have received a copy of the GNU Lesser General Public License
// * along with this program. If not, see <http://www.gnu.org/licenses/>.
// * --------------------------------------------------------------------------------------------------------------------
// */

//using System.Collections.Generic;
//using System.Threading.Tasks;
//using JsonRpc.Standard.Contracts;

//namespace AI4E.Modularity.Debugging
//{
//    public interface IEndPointRouterStub
//    {
//        [JsonRpcMethod("init")]
//        Task Init(string localEndPoint);

//        [JsonRpcMethod("receive")]
//        Task<byte[]> ReceiveAsync(string localEndPoint);

//        [JsonRpcMethod("getRoutes")]
//        Task<List<string>> GetRoutesAsync(string localEndPoint, string messageType);

//        [JsonRpcMethod("registerRoute")]
//        Task RegisterRouteAsync(string localEndPoint, string messageType);

//        [JsonRpcMethod("send1")]
//        Task SendAsync(string localEndPoint, byte[] message, string route);

//        [JsonRpcMethod("send2")]
//        Task SendAsync(string localEndPoint, byte[] response, byte[] request);

//        [JsonRpcMethod("unregisterRoute")]
//        Task UnregisterRouteAsync(string localEndPoint, string messageType);

//        [JsonRpcMethod("renewLease")]
//        Task RenewLease(string localEndPoint);
//    }
//}
