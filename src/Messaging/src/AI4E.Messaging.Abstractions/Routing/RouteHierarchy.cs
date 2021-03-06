using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using AI4E.Utils;

namespace AI4E.Messaging.Routing
{
#pragma warning disable CA1710
    public readonly struct RouteHierarchy : IEquatable<RouteHierarchy>, IEnumerable<Route>, IReadOnlyList<Route>
#pragma warning restore CA1710
    {
        private readonly ImmutableArray<Route> _routes;

        public RouteHierarchy(IEnumerable<Route> routes)
        {
            _routes = routes.ToImmutableArray();
        }

        public RouteHierarchy(ImmutableArray<Route> routes)
        {
            _routes = routes;
        }

        Route IReadOnlyList<Route>.this[int index] => _routes[index];

        public ref readonly Route this[int index] => ref _routes.ItemRef(index);

        public int Count => _routes.Length;

        public bool Equals(RouteHierarchy other)
        {
            return Equals(in other);
        }

        public bool Equals(in RouteHierarchy other)
        {
            if (_routes.IsDefaultOrEmpty)
            {
                return other._routes.IsDefaultOrEmpty;
            }

            if (_routes.Length != other._routes.Length)
            {
                return false;
            }

            for (var i = 0; i < _routes.Length; i++)
            {
                ref readonly var routeA = ref _routes.ItemRef(i);
                ref readonly var routeB = ref _routes.ItemRef(i);

                if (!routeA.Equals(in routeB))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteHierarchy routeHierarchy && Equals(in routeHierarchy);
        }

        public override int GetHashCode()
        {
            if (_routes.IsDefaultOrEmpty)
                return 0;

            return _routes.AsSpan().SequenceHashCode();
        }

        public override string ToString()
        {
            if (_routes.IsDefaultOrEmpty)
            {
                return "<No routes>";
            }

            var resultBuilder = new StringBuilder();
            resultBuilder.Append('<');
            for (var i = 0; i < _routes.Length; i++)
            {
                if (i > 0)
                {
                    resultBuilder.Append(' ');
                    resultBuilder.Append(',');
                }
                resultBuilder.Append(_routes[i].ToString());
            }

            resultBuilder.Append('>');

            return resultBuilder.ToString();
        }

        public static bool operator ==(in RouteHierarchy left, in RouteHierarchy right)
        {
            return left.Equals(in right);
        }

        public static bool operator !=(in RouteHierarchy left, in RouteHierarchy right)
        {
            return !left.Equals(in right);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_routes.GetEnumerator());
        }

        IEnumerator<Route> IEnumerable<Route>.GetEnumerator()
        {
#pragma warning disable HAA0401, HAA0601
            return ((IEnumerable<Route>)_routes).GetEnumerator();
#pragma warning restore HAA0401, HAA0601
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
#pragma warning disable HAA0401, HAA0601
            return ((IEnumerable<Route>)_routes).GetEnumerator();
#pragma warning restore HAA0401, HAA0601
        }

        public struct Enumerator : IEnumerator<Route>, IEnumerator
        {
            // This MUST NOT be marked readonly, to allow the compiler to access this field by reference.
            private ImmutableArray<Route>.Enumerator _underlying;

            internal Enumerator(ImmutableArray<Route>.Enumerator underlying)
            {
                _underlying = underlying;
            }

            public Route Current => _underlying.Current;

#pragma warning disable HAA0601
            object IEnumerator.Current => Current;
#pragma warning restore HAA0601

            public bool MoveNext()
            {
                return _underlying.MoveNext();
            }

            public void Dispose() { }

            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }
        }

        public static void Write(BinaryWriter writer, in RouteHierarchy routeHierarchy)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            PrefixCodingHelper.Write7BitEncodedInt(writer, routeHierarchy.Count);

            for (var i = 0; i < routeHierarchy.Count; i++)
            {
                Route.Write(writer, routeHierarchy[i]);
            }
        }

        public static void Read(BinaryReader reader, out RouteHierarchy routeHierarchy)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            var count = PrefixCodingHelper.Read7BitEncodedInt(reader);
            var routesBuilder = ImmutableArray.CreateBuilder<Route>(initialCapacity: count);
            routesBuilder.Count = count;

            for (var i = 0; i < count; i++)
            {
                Route.Read(reader, out Unsafe.AsRef(routesBuilder.ItemRef(i)));
            }

            Debug.Assert(routesBuilder.Capacity == routesBuilder.Count);
            routesBuilder.Capacity = routesBuilder.Count;
            routeHierarchy = new RouteHierarchy(routesBuilder.MoveToImmutable());
        }
    }
}
