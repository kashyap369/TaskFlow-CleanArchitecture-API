namespace TaskFlow.Application.Contracts.Security
{
    public interface ICurrentUserService
    {
        int UserId { get; }

        string Email { get; }

        /// <summary>
        /// The caller's IP address. Never throws — returns
        /// "unknown" when it cannot be resolved. Used for
        /// refresh token auditing (CreatedByIp / RevokedByIp).
        /// </summary>
        string IpAddress { get; }
    }
}
