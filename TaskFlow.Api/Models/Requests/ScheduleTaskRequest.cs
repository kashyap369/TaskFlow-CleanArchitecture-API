namespace TaskFlow.Api.Models.Requests;

public sealed record ScheduleTaskRequest(
    DateTime StartDate,
    DateTime? ExpectedCompletionDate);
