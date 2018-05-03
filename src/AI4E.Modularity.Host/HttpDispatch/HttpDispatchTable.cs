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

//using System;
//using System.Collections.Immutable;
//using System.Linq;
//using System.Threading;
//using AI4E.Routing;

//namespace AI4E.Modularity.HttpDispatch
//{
//    public sealed class HttpDispatchTable
//    {
//        private volatile ImmutableDictionary<string, EndPointRoute> _entries = ImmutableDictionary<string, EndPointRoute>.Empty;

//        public HttpDispatchTable() { }

//        public bool Register(string prefix, EndPointRoute endPoint)
//        {
//            ImmutableDictionary<string, EndPointRoute> current = _entries,
//                                                       start,
//                                                       desired;

//            do
//            {
//                start = current;

//                desired = start.Add(prefix, endPoint);

//                if (start == desired)
//                    return false;

//                current = Interlocked.CompareExchange(ref _entries, desired, start);
//            }
//            while (start != current);

//            return true;
//        }

//        public void Unregister(EndPointRoute endPoint)
//        {
//            ImmutableDictionary<string, EndPointRoute> current = _entries,
//                                                       start,
//                                                       desired;

//            do
//            {
//                start = current;

//                desired = start.Where(p => p.Value != endPoint).ToImmutableDictionary();

//                if (start == desired)
//                    return;

//                current = Interlocked.CompareExchange(ref _entries, desired, start);
//            }
//            while (start != current);
//        }

//        public bool Unregister(string prefix)
//        {
//            ImmutableDictionary<string, EndPointRoute> current = _entries,
//                                                       start,
//                                                       desired;

//            do
//            {
//                start = current;

//                desired = start.Remove(prefix);

//                if (start == desired)
//                    return false;

//                current = Interlocked.CompareExchange(ref _entries, desired, start);
//            }
//            while (start != current);

//            return true;
//        }

//        public bool TryGetEndPoint(string path, out EndPointRoute endPoint)
//        {
//            var pathSegments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
//            var bestMatching = default(EndPointRoute);
//            var matchingLength = 0;

//            foreach (var (key, value) in _entries.Select(p => (p.Key, p.Value)))
//            {
//                var prefixSegments = key.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

//                if (prefixSegments.Length <= pathSegments.Length)
//                {
//                    for (var i = 0; i < prefixSegments.Length; i++)
//                    {
//                        if (!prefixSegments[i].Equals(pathSegments[i], StringComparison.InvariantCultureIgnoreCase))
//                            break;

//                        if (i == prefixSegments.Length - 1 && matchingLength < prefixSegments.Length)
//                        {
//                            matchingLength = prefixSegments.Length;
//                            bestMatching = value;
//                        }
//                    }
//                }
//            }

//            endPoint = bestMatching;
//            return matchingLength > 0;
//        }
//    }
//}
