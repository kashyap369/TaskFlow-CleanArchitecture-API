using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.SetTaskEstimate;

public sealed record SetTaskEstimateCommand(
    int TaskId,
    int? EstimateMinutes
) : IRequest;
