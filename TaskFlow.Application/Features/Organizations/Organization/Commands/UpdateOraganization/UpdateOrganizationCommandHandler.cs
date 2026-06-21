using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.UpdateOraganization
{
    public sealed class UpdateOrganizationCommandHandler
        : IRequestHandler<UpdateOrganizationCommand>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateOrganizationCommandHandler(
            IOrganizationRepository organizationRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateOrganizationCommand request,
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