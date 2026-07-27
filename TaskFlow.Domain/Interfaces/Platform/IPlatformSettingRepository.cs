using TaskFlow.Domain.Entities.Platform;

namespace TaskFlow.Domain.Interfaces.Platform
{
    public interface IPlatformSettingRepository
    {
        /// <summary>
        /// The single settings row. Returns null only if the seeder
        /// has not run — every caller should treat that as a fault,
        /// not a normal empty case.
        /// </summary>
        Task<PlatformSetting?> GetAsync(
            CancellationToken cancellationToken = default);

        void Update(
            PlatformSetting setting);
    }
}
