namespace TaskFlow.Application.Contracts.Tracking;

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>A road-following shape: an encoded polyline (precision 6) and its length.</summary>
public sealed record MatchedRoute(string EncodedPolyline, double DistanceMeters);

/// <summary>
/// How a stretch was travelled, which decides the rules the matcher applies to it. Matching a
/// walk as a car is what put a U-turn on a one-way street the member simply walked down.
/// </summary>
public enum RouteTravelMode
{
    Walking,
    Driving
}

/// <summary>
/// Snaps GPS tracks onto the road network (Valhalla in production). Lives behind an interface so
/// the route pipeline stays testable and the engine can be swapped (e.g. Google Roads).
/// </summary>
public interface IRouteMatcher
{
    /// <summary>False when no engine is configured: the raw, cleaned track is kept instead.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Matches recorded points to roads under the rules of <paramref name="mode"/>. Null when the
    /// engine could not match this input — which is itself the answer that the stretch was not
    /// road travel.
    /// </summary>
    /// <exception cref="RouteMatcherUnavailableException">The engine did not answer; try again later.</exception>
    Task<MatchedRoute?> MatchAsync(IReadOnlyList<GeoPoint> points, RouteTravelMode mode, CancellationToken cancellationToken);
}

public sealed class RouteMatcherUnavailableException : Exception
{
    public RouteMatcherUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
