namespace TaskFlow.Domain.Enums.Organization;

public enum TrackedSegmentKind
{
    // Drawn from points the phone actually recorded.
    Recorded = 1,
    // Bridges a gap with no points (phone off, app killed, no signal). The road between is the
    // server's guess and must always be shown as one — dashed, never as if recorded.
    Inferred = 2
}
