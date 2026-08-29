using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.SetMemberCapacity;

public sealed record SetMemberCapacityCommand(
    int OrganizationId,
    int UserId,
    int? WeeklyCapacityMinutes
) : IRequest;
