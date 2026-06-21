using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.DeleteRole
{
    public sealed class DeleteRoleCommandHandler
        : IRequestHandler<DeleteRoleCommand>
    {
        private readonly IOrganizationRoleRepository _organizationRoleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteRoleCommandHandler(
            IOrganizationRoleRepository organizationRoleRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRoleRepository = organizationRoleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteRoleCommand request,
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

            _organizationRoleRepository.Remove(role);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}