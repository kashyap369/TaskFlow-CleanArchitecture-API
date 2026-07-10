namespace TaskFlow.Application.Contracts.Security
{
    public interface ICurrentUserService
    {
        int UserId { get; }

        string Email { get; }
    }
}
