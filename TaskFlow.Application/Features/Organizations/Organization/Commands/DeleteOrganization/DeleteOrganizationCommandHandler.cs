using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.DeleteOrganization
{
    public sealed class DeleteOrganizationCommandHandler
        : IRequestHandler<DeleteOrganizationCommand>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteOrganizationCommandHandler(
            IOrganizationRepository organizationRepository,
            IOrganizationAccessGuard accessGuard,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _accessGuard = accessGuard;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteOrganizationCommand request,
            CancellationToken cancellationToken)
        {
            // Owner-only — see UpdateOrganizationCommandHandler.
            // Deleting an entire workspace is the single most
            // destructive action in the product; it was previously
            // reachable by any authenticated user with an id.
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

            _organizationRepository.Remove(
                organization);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}