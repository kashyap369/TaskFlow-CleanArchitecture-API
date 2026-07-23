using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.LogManualWork
{
    public sealed record LogManualWorkCommand(
        int TaskId,
        DateTime StartedAt,
        DateTime EndedAt,
        string? Notes
    ) : IRequest<int>;
}
