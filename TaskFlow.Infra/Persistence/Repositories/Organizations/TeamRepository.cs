using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations
{
    public sealed class TeamRepository
        : ITeamRepository
    {
        private readonly TaskFlowDbContext _context;

        public TeamRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<Team?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Teams
                .Include(x => x.Members)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Team>>
            GetByOrganizationIdAsync(
                int organizationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Teams
                .Where(x => x.OrganizationId == organizationId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            int organizationId,
            string name,
            CancellationToken cancellationToken = default)
        {
            return await _context.Teams
                .AsNoTracking()
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.Name == name,
                    cancellationToken);
        }

        public async Task AddAsync(
            Team team,
            CancellationToken cancellationToken = default)
        {
            await _context.Teams
                .AddAsync(
                    team,
                    cancellationToken);
        }

        public void Update(
            Team team)
        {
            _context.Teams.Update(team);
        }

        public void Remove(
            Team team)
        {
            team.SoftDelete();

            _context.Teams.Update(team);
        }
    }
}
