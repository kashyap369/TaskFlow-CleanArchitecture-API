using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums.Identity;

namespace TaskFlow.Domain.Interfaces.Identity.Users
{
    public interface IOneTimeCodeRepository
    {
        Task<OneTimeCode?> GetLatestAsync(
            int userId,
            OneTimeCodePurpose purpose,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OneTimeCode>> GetUnconsumedAsync(
            int userId,
            OneTimeCodePurpose purpose,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            OneTimeCode oneTimeCode,
            CancellationToken cancellationToken = default);

        void Update(OneTimeCode oneTimeCode);
    }
}
