using System;
using System.Collections.Generic;
using AI4E.Utils;

namespace AI4E.Routing
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

        public override bool Equals(object obj)
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
            var hashCode = 2033542754;
            hashCode = hashCode * -1521134295 + EqualityComparer<Route>.Default.GetHashCode(Route);
            hashCode = hashCode * -1521134295 + RegistrationOptions.GetHashCode();
            return hashCode;
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
