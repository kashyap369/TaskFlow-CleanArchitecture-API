using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.DeleteOrganization
{
    public sealed class DeleteOrganizationCommandHandler
        : IRequestHandler<DeleteOrganizationCommand>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteOrganizationCommandHandler(
            IOrganizationRepository organizationRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteOrganizationCommand request,
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

            _organizationRepository.Remove(
                organization);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}