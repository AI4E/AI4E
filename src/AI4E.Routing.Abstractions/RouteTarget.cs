using System;
using System.Collections.Generic;
using AI4E.Utils;

namespace AI4E.Routing
{
    public readonly struct RouteTarget : IEquatable<RouteTarget>
    {
        public RouteTarget(EndPointAddress endPoint, RouteRegistrationOptions registrationOptions)
        {
            if (endPoint == default)
            {
                this = default;
                return;
            }

            if (!registrationOptions.IsValid())
                throw new ArgumentException("Invalid enum valid.", nameof(registrationOptions));

            EndPoint = endPoint;
            RegistrationOptions = registrationOptions;
        }

        public EndPointAddress EndPoint { get; }
        public RouteRegistrationOptions RegistrationOptions { get; }

        public override bool Equals(object obj)
        {
            return obj is RouteTarget routeTarget && Equals(routeTarget);
        }

        public bool Equals(RouteTarget other)
        {
            return other.EndPoint == EndPoint &&
                   other.RegistrationOptions == RegistrationOptions;
        }

        public override int GetHashCode()
        {
            var hashCode = 2033542754;
            hashCode = hashCode * -1521134295 + EqualityComparer<EndPointAddress>.Default.GetHashCode(EndPoint);
            hashCode = hashCode * -1521134295 + RegistrationOptions.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(RouteTarget left, RouteTarget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RouteTarget left, RouteTarget right)
        {
            return !left.Equals(right);
        }

        public void Deconstruct(out EndPointAddress endPoint, out RouteRegistrationOptions registrationOptions)
        {
            endPoint = EndPoint;
            registrationOptions = RegistrationOptions;
        }
    }
}
