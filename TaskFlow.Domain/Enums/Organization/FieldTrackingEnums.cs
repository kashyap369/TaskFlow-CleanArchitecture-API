namespace TaskFlow.Domain.Enums.Organization;

public enum TrackedSegmentKind
{
    // Drawn from points the phone actually recorded.
    Recorded = 1,
    // Bridges a gap with no points (phone off, app killed, no signal). A straight line between
    // where the points stop and where they resume: the server does not know the way taken, and
    // must never draw a road as if it did. Always shown as a guess — dashed.
    Inferred = 2,
    // Recorded travel that does not belong to the road network: a train, a metro, a ferry, or any
    // stretch the matcher could only place on a road far from where the member actually was.
    // Drawn from the member's own points, never snapped. Real travel, so it counts as travelled.
    OffRoad = 3
}
