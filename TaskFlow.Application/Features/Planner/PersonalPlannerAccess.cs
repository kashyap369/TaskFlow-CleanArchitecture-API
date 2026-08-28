using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner;

internal static class PersonalPlannerAccess
{
    public static async Task<Project> GetOwnedProjectAsync(
        int projectId,
        IProjectRepository projectRepository,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            throw new NotFoundException("PROJECT_NOT_FOUND", "Project not found.");
        }

        if (!project.IsPersonal || project.CreatedByUserId != currentUserService.UserId)
        {
            throw new ForbiddenException(
                "PLANNER_ACCESS_DENIED",
                "Planner currently supports only personal projects owned by you.");
        }

        return project;
    }
}
