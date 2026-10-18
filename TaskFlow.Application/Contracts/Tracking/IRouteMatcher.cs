namespace TaskFlow.Application.Contracts.Tracking;

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>A road-following shape: an encoded polyline (precision 6) and its length.</summary>
public sealed record MatchedRoute(string EncodedPolyline, double DistanceMeters);

/// <summary>
/// Snaps GPS tracks onto the road network (Valhalla in production). Lives behind an interface so
/// the route pipeline stays testable and the engine can be swapped (e.g. Google Roads).
/// </summary>
public interface IRouteMatcher
{
    /// <summary>False when no engine is configured: the raw, cleaned track is kept instead.</summary>
    bool IsConfigured { get; }

    /// <summary>Matches recorded points to roads. Null when the engine could not match this input.</summary>
    /// <exception cref="RouteMatcherUnavailableException">The engine did not answer; try again later.</exception>
    Task<MatchedRoute?> MatchAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken);

    /// <summary>The likely road between two points, for a gap with no recorded points. Null when there is none.</summary>
    /// <exception cref="RouteMatcherUnavailableException">The engine did not answer; try again later.</exception>
    Task<MatchedRoute?> RouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken);
}

public sealed class RouteMatcherUnavailableException : Exception
{
    public RouteMatcherUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
