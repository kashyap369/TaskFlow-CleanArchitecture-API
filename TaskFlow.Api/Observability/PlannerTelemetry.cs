using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TaskFlow.Api.Observability;

public static class PlannerTelemetry
{
    public const string SourceName = "TaskFlow.Planner";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("taskflow.planner.requests");
    public static readonly Counter<long> Failures = Meter.CreateCounter<long>("taskflow.planner.failures");
    public static readonly Counter<long> Conflicts = Meter.CreateCounter<long>("taskflow.planner.conflicts");
    public static readonly Counter<long> Mutations = Meter.CreateCounter<long>("taskflow.planner.mutations");
    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("taskflow.planner.request.duration", "ms");
}
