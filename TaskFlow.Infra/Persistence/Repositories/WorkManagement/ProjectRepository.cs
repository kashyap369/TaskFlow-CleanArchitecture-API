using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.WorkManagement
{
    public sealed class ProjectRepository
        : IProjectRepository
    {
        private readonly TaskFlowDbContext _context;

        public ProjectRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<Project?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Projects
                .Include(x => x.Tasks)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Project>>
            GetByOrganizationIdAsync(
                int organizationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Projects
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Project>>
            GetByCreatedByUserIdAsync(
                int userId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Projects
                .Where(x => x.CreatedByUserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            int organizationId,
            string title,
            CancellationToken cancellationToken = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.Title == title,
                    cancellationToken);
        }

        public async Task AddAsync(
            Project project,
            CancellationToken cancellationToken = default)
        {
            await _context.Projects
                .AddAsync(
                    project,
                    cancellationToken);
        }

        public void Update(
            Project project)
        {
            _context.Projects.Update(project);
        }

        public void Remove(
            Project project)
        {
            project.SoftDelete();

            _context.Projects.Update(project);
        }

        public async Task<Project?> GetByNameAsync(
     int organizationId,
     string title,
     CancellationToken cancellationToken = default)
        {
            var normalizedTitle = title.Trim().ToLower();

            return await _context.Projects
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == organizationId
                      && x.Title.ToLower() == normalizedTitle,
                    cancellationToken);
        }

    }
}