using MediatR;
using TaskFlow.Application.Contracts.Security;
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
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateOrganizationCommandHandler(
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateOrganizationCommand request,
            CancellationToken cancellationToken)
        {
            // The owner is always the logged-in user (taken
            // from the JWT), never from the request body.
            var ownerUserId =
                _currentUserService.UserId;

            var owner =
                await _userRepository.GetByIdAsync(
                    ownerUserId,
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
                    ownerUserId);

            await _organizationRepository.AddAsync(
                organization,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return organization.Id;
        }
    }
}