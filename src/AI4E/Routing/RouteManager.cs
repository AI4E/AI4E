//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using AI4E.Coordination;
//using static System.Diagnostics.Debug;

//namespace AI4E.Routing
//{
//    public sealed class RouteManager : IRouteStore
//    {
//        private const string _routesRootPath = "/routes";

//        private static readonly string[] _seperatorStrings = { "/", "\\" };
//        private static readonly char[] _seperatorChars = { '/', '\\' };
//        private const char _escapeChar = '-';
//        private const string _escapeString = "-";

//        private const string _escapedEscapeString = "--";
//        private const string _escapedSeperatorString = "-/";


//        private readonly ICoordinationManager _coordinationManager;

//        public RouteManager(ICoordinationManager coordinationManager)
//        {
//            if (coordinationManager == null)
//                throw new ArgumentNullException(nameof(coordinationManager));

//            _coordinationManager = coordinationManager;
//        }

//        private static string GetPath(string messageType)
//        {
//            var messageTypeB = new StringBuilder(messageType.Length + CountCharsToEscape(messageType));
//            messageTypeB.Append(messageType);
//            Escape(messageTypeB, startIndex: 0);

//            return EntryPathHelper.GetChildPath(_routesRootPath, messageType.ToString(), normalize: false);
//        }

//        private static int CountCharsToEscape(string str)
//        {
//            return str.Count(p => _seperatorChars.Contains(p) || p == _escapeChar);
//        }

//        private static string GetPath(string messageType, string route, string session)
//        {
//            return EntryPathHelper.GetChildPath(GetPath(messageType), GetEntryName(route, session));
//        }

//        // Gets the entry name that is roughly {route}-){session}
//        private static string GetEntryName(string route, string session)
//        {
//            var resultsBuilder = new StringBuilder(route.Length +
//                                                   session.Length +
//                                                   CountCharsToEscape(route) +
//                                                   CountCharsToEscape(session) +
//                                                   1);
//            resultsBuilder.Append(route);

//            Escape(resultsBuilder, 0);

//            var sepIndex = resultsBuilder.Length;

//            resultsBuilder.Append(' ');
//            resultsBuilder.Append(' ');

//            resultsBuilder.Append(session);

//            Escape(resultsBuilder, sepIndex + 2);

//            resultsBuilder[sepIndex] = _escapeChar;
//            resultsBuilder[sepIndex + 1] = ')'; // We need to ensure that the created entry is unique. Append any char that is neither - nor /

//            return resultsBuilder.ToString();

//        }

//        private static void Escape(StringBuilder str, int startIndex)
//        {
//            // Replace all occurances of - with --
//            str.Replace(_escapeString, _escapedEscapeString, startIndex, str.Length - startIndex);

//            // Replace all occurances of / and \ with -/
//            foreach (var seperator in _seperatorStrings)
//            {
//                str.Replace(seperator, _escapedSeperatorString, startIndex, str.Length - startIndex);
//            }
//        }

//        public Task<bool> AddRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
//        {
//            if (localEndPoint == null)
//                throw new ArgumentNullException(nameof(localEndPoint));

//            if (string.IsNullOrWhiteSpace(messageType))
//                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));


//        }

//        public Task<bool> RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
//        {
//            if (localEndPoint == null)
//                throw new ArgumentNullException(nameof(localEndPoint));

//            if (string.IsNullOrWhiteSpace(messageType))
//                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));


//        }

//        public Task RemoveRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
//        {
//            if (localEndPoint == null)
//                throw new ArgumentNullException(nameof(localEndPoint));


//        }

//        public Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
//        {
//            if (messageType == null)
//                throw new ArgumentNullException(nameof(messageType));

//            if (string.IsNullOrWhiteSpace(messageType))
//                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));


//        }

//        public Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
//        {

//        }
//    }
//}
