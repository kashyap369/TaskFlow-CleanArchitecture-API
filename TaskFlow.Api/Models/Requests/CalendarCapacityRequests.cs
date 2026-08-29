namespace TaskFlow.Api.Models.Requests;

public sealed record SetTaskEstimateRequest(int? EstimateMinutes);

public sealed record SetMemberCapacityRequest(int? WeeklyCapacityMinutes);
