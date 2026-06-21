using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.UpdateRole
{
    public sealed class UpdateRoleCommandHandler
        : IRequestHandler<UpdateRoleCommand>
    {
        private readonly IOrganizationRoleRepository _organizationRoleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateRoleCommandHandler(
            IOrganizationRoleRepository organizationRoleRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRoleRepository = organizationRoleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateRoleCommand request,
            CancellationToken cancellationToken)
        {
            var role =
                await _organizationRoleRepository.GetByIdAsync(
                    request.RoleId,
                    cancellationToken);

            if (role is null)
            {
                throw new NotFoundException(
                    "ROLE_NOT_FOUND",
                    "Organization role not found.");
            }

            var roleWithSameName =
                await _organizationRoleRepository.GetByNameAsync(
                    role.OrganizationId,
                    request.Name,
                    cancellationToken);

            if (roleWithSameName is not null &&
                roleWithSameName.Id != role.Id)
            {
                throw new ConflictException(
                    "ROLE_ALREADY_EXISTS",
                    "Role name already exists.");
            }

            role.Update(
                request.Name,
                request.Description);

            _organizationRoleRepository.Update(
                role);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}