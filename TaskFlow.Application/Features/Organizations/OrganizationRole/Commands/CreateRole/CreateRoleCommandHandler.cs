using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.CreateRole
{
    public sealed class CreateRoleCommandHandler
        : IRequestHandler<CreateRoleCommand, int>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationRoleRepository _organizationRoleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateRoleCommandHandler(
            IOrganizationRepository organizationRepository,
            IOrganizationRoleRepository organizationRoleRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _organizationRoleRepository = organizationRoleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateRoleCommand request,
            CancellationToken cancellationToken)
        {
            var organization =
                await _organizationRepository.GetByIdAsync(
                    request.OrganizationId,
                    cancellationToken);

            if (organization is null)
            {
                throw new NotFoundException(
                    "ORGANIZATION_NOT_FOUND",
                    "Organization not found.");
            }

            var roleExists =
                await _organizationRoleRepository
                    .ExistsByNameAsync(
                        request.OrganizationId,
                        request.Name,
                        cancellationToken);

            if (roleExists)
            {
                throw new ConflictException(
                    "ROLE_ALREADY_EXISTS",
                    "Role already exists in the organization.");
            }

            var role = new Domain.Entities.Organization.OrganizationRole(
                request.OrganizationId,
                request.Name,
                request.Description);

            await _organizationRoleRepository.AddAsync(
                role,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return role.Id;
        }
    }
}