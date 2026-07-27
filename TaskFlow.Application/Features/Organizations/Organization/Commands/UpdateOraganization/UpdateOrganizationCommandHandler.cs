using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.UpdateOraganization
{
    public sealed class UpdateOrganizationCommandHandler
        : IRequestHandler<UpdateOrganizationCommand>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateOrganizationCommandHandler(
            IOrganizationRepository organizationRepository,
            IOrganizationAccessGuard accessGuard,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _accessGuard = accessGuard;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateOrganizationCommand request,
            CancellationToken cancellationToken)
        {
            // Owner-only. This command carries no scoped-request
            // marker (those cover reads), so without this call the
            // handler checked existence and nothing else — any
            // authenticated user could rename any organization.
            await _accessGuard.EnsureOrganizationOwnerAsync(
                request.OrganizationId,
                cancellationToken);

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

            var existingOrganization =
                await _organizationRepository.GetByNameAsync(
                    request.Name,
                    cancellationToken);

            if (existingOrganization is not null &&
                existingOrganization.Id != organization.Id)
            {
                throw new ConflictException(
                    "ORGANIZATION_NAME_ALREADY_EXISTS",
                    "Organization name already exists.");
            }

            organization.UpdateDetails(
                request.Name,
                request.Description);

            _organizationRepository.Update(
                organization);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}