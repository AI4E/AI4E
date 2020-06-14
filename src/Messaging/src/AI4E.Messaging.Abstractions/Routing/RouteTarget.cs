using System;
using AI4E.Utils;

namespace AI4E.Messaging.Routing
{
    public readonly struct RouteTarget : IEquatable<RouteTarget>
    {
        public RouteTarget(RouteEndPointAddress endPoint, RouteRegistrationOptions registrationOptions)
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

        public RouteEndPointAddress EndPoint { get; }
        public RouteRegistrationOptions RegistrationOptions { get; }

        public override bool Equals(object? obj)
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
            return (EndPoint, RegistrationOptions).GetHashCode();
        }

        public static bool operator ==(RouteTarget left, RouteTarget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RouteTarget left, RouteTarget right)
        {
            return !left.Equals(right);
        }

        public void Deconstruct(out RouteEndPointAddress endPoint, out RouteRegistrationOptions registrationOptions)
        {
            endPoint = EndPoint;
            registrationOptions = RegistrationOptions;
        }
    }
}
