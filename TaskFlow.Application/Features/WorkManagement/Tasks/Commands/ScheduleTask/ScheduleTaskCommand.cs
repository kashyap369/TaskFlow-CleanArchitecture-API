using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ScheduleTask;

public sealed record ScheduleTaskCommand(
    int TaskId,
    DateTime StartDate,
    DateTime? ExpectedCompletionDate
) : IRequest;
