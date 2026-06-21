using Microsoft.EntityFrameworkCore;
using System.Reflection;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Entities.WorkManagement.SubTasks;
using TaskFlow.Infra.DomainEvents.Dispatchers;
using Task = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;
namespace TaskFlow.Infra.Persistence.Context
{
    public sealed class TaskFlowDbContext : DbContext
    {
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options,IDomainEventDispatcher domainEventDispatcher): base(options)
        {
            _domainEventDispatcher = domainEventDispatcher;
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();

        public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

        public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

        public DbSet<Project> Projects => Set<Project>();
        public DbSet<SystemRole> SystemRoles => Set<SystemRole>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Task> Tasks => Set<Task>();

        public DbSet<SubTask> SubTasks => Set<SubTask>();

        public DbSet<UserRole> UserRoles => Set<UserRole>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);

            ApplySoftDeleteQueryFilter(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entitiesWithDomainEvents = ChangeTracker
                .Entries<BaseEntity>()
                .Where(x => x.Entity.DomainEvents.Any())
                .ToList();

            var domainEvents = entitiesWithDomainEvents
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();

            var result = await base.SaveChangesAsync(
                cancellationToken);

            await _domainEventDispatcher.DispatchAsync(
                domainEvents,
                cancellationToken);

            foreach (var entityEntry in entitiesWithDomainEvents)
            {
                entityEntry.Entity.ClearDomainEvents();
            }

            return result;
        }

        private static void ApplySoftDeleteQueryFilter(
            ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(TaskFlowDbContext)
                        .GetMethod(
                            nameof(SetSoftDeleteFilter),
                            BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(entityType.ClrType);

                    method.Invoke(null, new object[] { modelBuilder });
                }
            }
        }

        private static void SetSoftDeleteFilter<TEntity>(
            ModelBuilder modelBuilder)
            where TEntity : AuditableEntity
        {
            modelBuilder.Entity<TEntity>()
                .HasQueryFilter(x => !x.IsDeleted);
        }
    }
}