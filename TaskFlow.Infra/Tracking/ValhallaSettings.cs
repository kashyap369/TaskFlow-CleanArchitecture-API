namespace TaskFlow.Infra.Tracking;

/// <summary>
/// The road-matching engine (Valhalla). Internal only: the API reaches it on the Docker network,
/// never through a public port. Empty <see cref="BaseUrl"/> = not configured; routes are then
/// drawn from the cleaned GPS track without snapping, and nothing fails.
/// </summary>
public sealed class ValhallaSettings
{
    public const string SectionName = "Valhalla";

    // e.g. http://valhalla:8002 — set as Valhalla__BaseUrl.
    public string BaseUrl { get; set; } = string.Empty;

    // Valhalla costing model: auto, motorcycle, motor_scooter, bicycle, pedestrian.
    public string Costing { get; set; } = "auto";

    public int TimeoutSeconds { get; set; } = 30;

    // How far (metres) from a fix the matcher may look for a road.
    public int SearchRadiusMeters { get; set; } = 50;
}
