using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Platform;

namespace TaskFlow.Application.Features.Platform.Commands.UpdatePlatformSettings
{
    public sealed class UpdatePlatformSettingsCommandHandler
        : IRequestHandler<UpdatePlatformSettingsCommand>
    {
        private readonly IPlatformSettingRepository _platformSettingRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePlatformSettingsCommandHandler(
            IPlatformSettingRepository platformSettingRepository,
            IUnitOfWork unitOfWork)
        {
            _platformSettingRepository = platformSettingRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdatePlatformSettingsCommand request,
            CancellationToken cancellationToken)
        {
            var settings =
                await _platformSettingRepository.GetAsync(
                    cancellationToken);

            if (settings is null)
            {
                throw new NotFoundException(
                    "PLATFORM_SETTINGS_NOT_FOUND",
                    "Platform settings have not been initialised.");
            }

            settings.Update(
                request.ApplicationName,
                request.SupportEmail,
                request.RegistrationOpen,
                request.MaintenanceMode,
                request.MaintenanceMessage);

            _platformSettingRepository.Update(settings);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
