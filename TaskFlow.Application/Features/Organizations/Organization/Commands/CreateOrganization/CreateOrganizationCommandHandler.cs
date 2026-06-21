using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.CreateOrganization
{
    public sealed class CreateOrganizationCommandHandler
        : IRequestHandler<CreateOrganizationCommand, int>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateOrganizationCommandHandler(
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateOrganizationCommand request,
            CancellationToken cancellationToken)
        {
            var owner =
                await _userRepository.GetByIdAsync(
                    request.OwnerUserId,
                    cancellationToken);

            if (owner is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            var organizationExists =
                await _organizationRepository
                    .ExistsByNameAsync(
                        request.Name,
                        cancellationToken);

            if (organizationExists)
            {
                throw new ConflictException(
                    "ORGANIZATION_ALREADY_EXISTS",
                    "Organization name already exists.");
            }

            var organization =
                new Domain.Entities.Organization.Organization(
                    request.Name,
                    request.Description,
                    request.OwnerUserId);

            await _organizationRepository.AddAsync(
                organization,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return organization.Id;
        }
    }
}