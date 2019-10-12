using System;
using AI4E.Utils;

namespace AI4E.Messaging.Routing
{
    public readonly struct RouteRegistration : IEquatable<RouteRegistration>
    {
        public RouteRegistration(Route route, RouteRegistrationOptions registrationOptions)
        {
            if (!registrationOptions.IsValid())
                throw new ArgumentException("Invalid enum valid.", nameof(registrationOptions));

            Route = route;
            RegistrationOptions = registrationOptions;
        }

        public Route Route { get; }
        public RouteRegistrationOptions RegistrationOptions { get; }

        public override bool Equals(object? obj)
        {
            return obj is RouteRegistration routeRegistration && Equals(routeRegistration);
        }

        public bool Equals(RouteRegistration other)
        {
            return other.Route == Route &&
                   other.RegistrationOptions == RegistrationOptions;
        }

        public override int GetHashCode()
        {
            return (Route, RegistrationOptions).GetHashCode();
        }

        public static bool operator ==(RouteRegistration left, RouteRegistration right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RouteRegistration left, RouteRegistration right)
        {
            return !left.Equals(right);
        }

        public void Deconstruct(out Route route, out RouteRegistrationOptions registrationOptions)
        {
            route = Route;
            registrationOptions = RegistrationOptions;
        }
    }
}
