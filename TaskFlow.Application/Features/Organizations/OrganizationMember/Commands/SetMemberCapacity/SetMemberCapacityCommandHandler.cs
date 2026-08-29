using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.SetMemberCapacity;

public sealed class SetMemberCapacityCommandHandler : IRequestHandler<SetMemberCapacityCommand>
{
    private readonly IOrganizationMemberRepository _members;
    private readonly IOrganizationPermissionChecker _permissionChecker;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SetMemberCapacityCommandHandler(
        IOrganizationMemberRepository members,
        IOrganizationPermissionChecker permissionChecker,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _members = members;
        _permissionChecker = permissionChecker;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SetMemberCapacityCommand request, CancellationToken cancellationToken)
    {
        await _permissionChecker.EnsurePermissionAsync(
            request.OrganizationId,
            _currentUser.UserId,
            OrganizationPermissionNames.ManageMembers,
            cancellationToken);

        var member = await _members.GetMemberAsync(
            request.OrganizationId,
            request.UserId,
            cancellationToken);
        if (member is null)
            throw new NotFoundException("MEMBER_NOT_FOUND", "Organization member not found.");

        member.SetWeeklyCapacity(request.WeeklyCapacityMinutes);
        _members.Update(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
