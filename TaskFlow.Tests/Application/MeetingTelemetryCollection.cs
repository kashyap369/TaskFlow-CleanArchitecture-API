namespace TaskFlow.Tests.Application;

/// <summary>
/// Phase 7 / P7.4. <c>MeetingTelemetry</c> publishes to a process-wide meter, so any test that
/// drives a meeting handler emits into every listening snapshot — including one another test is
/// asserting on. Test classes that emit meeting telemetry share this collection, which xUnit runs
/// sequentially, so a snapshot only ever observes the measurements its own test produced.
///
/// A test class that touches meeting handlers, the guest funnel, capacity or the media provider
/// belongs here. Domain-entity tests do not: entities emit no telemetry.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MeetingTelemetryCollection
{
    public const string Name = "meeting-telemetry";
}
