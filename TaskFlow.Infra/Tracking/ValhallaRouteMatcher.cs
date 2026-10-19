using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Tracking;
using TaskFlow.Application.Features.Attendance;

namespace TaskFlow.Infra.Tracking;

/// <summary>
/// Calls Valhalla's <c>trace_route</c> (map matching) and <c>route</c> endpoints. One long-lived
/// HttpClient: this is a singleton talking to one internal host.
/// </summary>
public sealed class ValhallaRouteMatcher : IRouteMatcher, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ValhallaSettings _settings;
    private readonly HttpClient? _http;
    private readonly ILogger<ValhallaRouteMatcher> _logger;

    public ValhallaRouteMatcher(IOptions<ValhallaSettings> settings, ILogger<ValhallaRouteMatcher> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        if (Uri.TryCreate(_settings.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
            {
                BaseAddress = new Uri(baseUri.ToString().TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
            };
        }
    }

    public bool IsConfigured => _http is not null;

    public Task<MatchedRoute?> MatchAsync(IReadOnlyList<GeoPoint> points, RouteTravelMode mode, CancellationToken cancellationToken)
    {
        var request = new
        {
            shape = points.Select(x => new { lat = x.Latitude, lon = x.Longitude }).ToArray(),
            costing = CostingFor(mode),
            shape_match = "map_snap",
            directions_type = "none",
            units = "kilometers",
            trace_options = new { search_radius = _settings.SearchRadiusMeters }
        };
        return SendAsync("trace_route", request, cancellationToken);
    }

    /// <summary>
    /// The Valhalla costing model for a travel mode. Walking is matched as a pedestrian so the
    /// matcher stops obeying one-way and turn restrictions that do not apply on foot; the
    /// configured <see cref="ValhallaSettings.Costing"/> remains the driving model, so an
    /// organization can still choose motorcycle or motor_scooter for its riders.
    /// </summary>
    private string CostingFor(RouteTravelMode mode) =>
        mode == RouteTravelMode.Walking ? "pedestrian" : _settings.Costing;

    private async Task<MatchedRoute?> SendAsync(string action, object request, CancellationToken cancellationToken)
    {
        if (_http is null) return null;
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(action, request, cancellationToken);
        }
        catch (HttpRequestException error)
        {
            throw new RouteMatcherUnavailableException($"Valhalla {action} could not be reached.", error);
        }
        catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RouteMatcherUnavailableException($"Valhalla {action} timed out.", error);
        }

        using (response)
        {
            // 4xx: Valhalla understood and refused this input (no road near the points, no path).
            // That is a property of the data, not an outage: keep the raw track.
            if (response.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Valhalla {Action} could not match a trace ({Status}): {Body}",
                    action, (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
                return null;
            }
            if (!response.IsSuccessStatusCode)
                throw new RouteMatcherUnavailableException($"Valhalla {action} answered {(int)response.StatusCode}.");

            var result = await response.Content.ReadFromJsonAsync<RouteResponse>(Json, cancellationToken);
            var legs = result?.Trip?.Legs;
            if (legs is null || legs.Count == 0) return null;

            // Several legs come back when the trace had breaks; stitch them into one line.
            var shape = new List<GeoPoint>();
            foreach (var leg in legs.Where(x => !string.IsNullOrEmpty(x.Shape)))
            {
                var decoded = Polyline.Decode(leg.Shape!);
                if (shape.Count > 0 && decoded.Count > 0 && shape[^1] == decoded[0]) decoded.RemoveAt(0);
                shape.AddRange(decoded);
            }
            if (shape.Count < 2) return null;
            var kilometres = result!.Trip!.Summary?.Length ?? legs.Sum(x => x.Summary?.Length ?? 0);
            return new MatchedRoute(Polyline.Encode(shape), kilometres * 1000);
        }
    }

    public void Dispose() => _http?.Dispose();

    private sealed class RouteResponse { public Trip? Trip { get; set; } }

    private sealed class Trip
    {
        public List<Leg>? Legs { get; set; }
        public Summary? Summary { get; set; }
    }

    private sealed class Leg
    {
        public string? Shape { get; set; }
        public Summary? Summary { get; set; }
    }

    private sealed class Summary { public double Length { get; set; } }
}
