using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Services
{
    public sealed class OneTimeCodeVerifier
    {
        private readonly IOneTimeCodeRepository _repository;
        private readonly IOneTimeCodeProtector _protector;
        private readonly IUnitOfWork _unitOfWork;

        public OneTimeCodeVerifier(
            IOneTimeCodeRepository repository,
            IOneTimeCodeProtector protector,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _protector = protector;
            _unitOfWork = unitOfWork;
        }

        public async Task<OneTimeCode> VerifyAsync(
            int userId,
            OneTimeCodePurpose purpose,
            string code,
            CancellationToken cancellationToken)
        {
            var item = await _repository.GetLatestAsync(
                userId,
                purpose,
                cancellationToken);
            var now = DateTime.UtcNow;

            if (item is null || !item.CanAttempt(now))
                throw InvalidCode();

            if (!_protector.Verify(userId, purpose, code, item.CodeHash))
            {
                item.RegisterFailedAttempt(now);
                _repository.Update(item);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw InvalidCode();
            }

            item.Consume(now);
            _repository.Update(item);
            return item;
        }

        private static UnauthorizedException InvalidCode() =>
            new(
                "INVALID_OR_EXPIRED_CODE",
                "The code is invalid or has expired. Request a new code and try again.");
    }
}
