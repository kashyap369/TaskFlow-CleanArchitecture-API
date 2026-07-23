namespace TaskFlow.Application.Common.Authorization
{
    // Marker interfaces for read requests that must be access-checked
    // by AccessGuardBehavior before their handler runs. A query
    // implements the one that matches how it is scoped; the behavior
    // resolves it to an organization and verifies the current user
    // may see it. Commands are NOT marked — their handlers enforce
    // permissions directly.

    public interface IOrganizationScopedRequest
    {
        int OrganizationId { get; }
    }

    public interface IProjectScopedRequest
    {
        int ProjectId { get; }
    }

    public interface ITaskScopedRequest
    {
        int TaskId { get; }
    }

    public interface ITeamScopedRequest
    {
        int TeamId { get; }
    }

    public interface IRoleScopedRequest
    {
        int OrganizationRoleId { get; }
    }

    public interface IUserScopedRequest
    {
        int UserId { get; }
    }

    public interface IMemberReportScopedRequest
    {
        int UserId { get; }
    }
}
