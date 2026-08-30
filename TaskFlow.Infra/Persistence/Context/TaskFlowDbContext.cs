using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection;
using TaskFlow.Application.Contracts.Planner;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Planner;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Entities.Platform;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Entities.WorkManagement.SubTasks;
using TaskFlow.Domain.Entities.WorkManagement.WorkLogs;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Infra.DomainEvents.Dispatchers;
using Task = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Infra.Persistence.Context;

public sealed class TaskFlowDbContext : DbContext
{
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequirementChangeContext _requirementChangeContext;

    public TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options,
        IDomainEventDispatcher domainEventDispatcher, ICurrentUserService currentUserService,
        IRequirementChangeContext requirementChangeContext) : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
        _currentUserService = currentUserService;
        _requirementChangeContext = requirementChangeContext;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OneTimeCode> OneTimeCodes => Set<OneTimeCode>();
    public DbSet<Task> Tasks => Set<Task>();
    public DbSet<SubTask> SubTasks => Set<SubTask>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<OrganizationPermission> OrganizationPermissions => Set<OrganizationPermission>();
    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();
    public DbSet<TaskWorkLog> TaskWorkLogs => Set<TaskWorkLog>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<PlannerBoard> PlannerBoards => Set<PlannerBoard>();
    public DbSet<PlannerSceneRevision> PlannerSceneRevisions => Set<PlannerSceneRevision>();
    public DbSet<PlannerNode> PlannerNodes => Set<PlannerNode>();
    public DbSet<PlannerTemplate> PlannerTemplates => Set<PlannerTemplate>();
    public DbSet<PlannerTemplateVersion> PlannerTemplateVersions => Set<PlannerTemplateVersion>();
    public DbSet<PlannerResource> PlannerResources => Set<PlannerResource>();
    public DbSet<PlannerAsset> PlannerAssets => Set<PlannerAsset>();
    public DbSet<RequirementBaseline> RequirementBaselines => Set<RequirementBaseline>();
    public DbSet<RequirementSnapshot> RequirementSnapshots => Set<RequirementSnapshot>();
    public DbSet<RequirementChange> RequirementChanges => Set<RequirementChange>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingBadgeDefinition> MeetingBadgeDefinitions => Set<MeetingBadgeDefinition>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingAccessLink> MeetingAccessLinks => Set<MeetingAccessLink>();
    public DbSet<MeetingAttendance> MeetingAttendance => Set<MeetingAttendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);
        ApplySoftDeleteQueryFilter(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithDomainEvents = ChangeTracker.Entries<BaseEntity>()
            .Where(x => x.Entity.DomainEvents.Any()).ToList();
        var domainEvents = entitiesWithDomainEvents.SelectMany(x => x.Entity.DomainEvents).ToList();
        var requirementChanges = await CaptureRequirementChangesAsync(cancellationToken);
        var ownsTransaction = requirementChanges.Count > 0 && Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;

        var result = await base.SaveChangesAsync(cancellationToken);
        if (requirementChanges.Count > 0)
        {
            var actorUserId = _currentUserService.UserId;
            foreach (var pending in requirementChanges)
                RequirementChanges.Add(new RequirementChange(pending.BaselineId, pending.EntityType,
                    pending.EntityId(), pending.ParentEntityId(), pending.ChangeType, pending.Title(),
                    pending.OldValuesJson, pending.NewValuesJson(), actorUserId, _requirementChangeContext.Reason));

            result += await base.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }

        await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        foreach (var entry in entitiesWithDomainEvents) entry.Entity.ClearDomainEvents();
        return result;
    }

    private async Task<IReadOnlyList<PendingRequirementChange>> CaptureRequirementChangesAsync(
        CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var candidates = new List<RequirementCandidate>();
        foreach (var entry in ChangeTracker.Entries<Project>())
        {
            if (entry.State is EntityState.Added or EntityState.Detached or EntityState.Unchanged) continue;
            var candidate = CandidateForProject(entry);
            if (candidate is not null) candidates.Add(candidate);
        }
        foreach (var entry in ChangeTracker.Entries<Task>())
        {
            if (entry.Entity.ProjectId is not int projectId || entry.State is EntityState.Detached or EntityState.Unchanged) continue;
            var candidate = CandidateForTask(entry, projectId);
            if (candidate is not null) candidates.Add(candidate);
        }
        foreach (var entry in ChangeTracker.Entries<SubTask>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;
            var projectId = await ResolveSubTaskProjectIdAsync(entry.Entity.TaskId, cancellationToken);
            if (!projectId.HasValue) continue;
            var candidate = CandidateForSubTask(entry, projectId.Value);
            if (candidate is not null) candidates.Add(candidate);
        }

        if (candidates.Count == 0) return Array.Empty<PendingRequirementChange>();
        var projectIds = candidates.Select(x => x.ProjectId).Distinct().ToList();
        var rows = await RequirementBaselines.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId))
            .OrderByDescending(x => x.BaselineNumber).ToListAsync(cancellationToken);
        var baselines = rows.GroupBy(x => x.ProjectId).ToDictionary(x => x.Key, x => x.First().Id);
        return candidates.Where(x => baselines.ContainsKey(x.ProjectId))
            .Select(x => new PendingRequirementChange(baselines[x.ProjectId], x.EntityType, x.EntityId,
                x.ParentEntityId, x.ChangeType, x.Title, x.OldValuesJson, x.NewValuesJson)).ToList();
    }

    private async Task<int?> ResolveSubTaskProjectIdAsync(int taskId, CancellationToken cancellationToken)
    {
        var tracked = ChangeTracker.Entries<Task>().FirstOrDefault(x => x.Entity.Id == taskId)?.Entity;
        if (tracked?.ProjectId is int projectId) return projectId;
        return await Tasks.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == taskId)
            .Select(x => x.ProjectId).FirstOrDefaultAsync(cancellationToken);
    }

    private static RequirementCandidate? CandidateForProject(EntityEntry<Project> entry)
    {
        var type = GetChangeType(entry);
        var oldJson = entry.State == EntityState.Added ? null : RequirementFields.Serialize(new Dictionary<string, object?>
        {
            ["title"] = entry.Property(x => x.Title).OriginalValue,
            ["description"] = entry.Property(x => x.Description).OriginalValue,
            ["expectedCompletionDate"] = entry.Property(x => x.ExpectedCompletionDate).OriginalValue,
            ["problemStatement"] = entry.Property(x => x.ProblemStatement).OriginalValue,
            ["budgetAmount"] = entry.Property(x => x.BudgetAmount).OriginalValue,
            ["budgetCurrency"] = entry.Property(x => x.BudgetCurrency).OriginalValue,
            ["approximateDurationWeeks"] = entry.Property(x => x.ApproximateDurationWeeks).OriginalValue,
        });
        var newJson = type == RequirementChangeType.Removed ? null : RequirementFields.ForProject(entry.Entity);
        if (type == RequirementChangeType.Changed && oldJson == newJson) return null;
        return new(entry.Entity.Id, RequirementEntityType.Project, () => entry.Entity.Id, () => null,
            type, () => entry.Entity.Title, oldJson, () => newJson);
    }

    private static RequirementCandidate? CandidateForTask(EntityEntry<Task> entry, int projectId)
    {
        var type = GetChangeType(entry);
        var oldJson = entry.State == EntityState.Added ? null : RequirementFields.Serialize(new Dictionary<string, object?>
        {
            ["title"] = entry.Property(x => x.Title).OriginalValue,
            ["description"] = entry.Property(x => x.Description).OriginalValue,
            ["priority"] = (int)entry.Property(x => x.Priority).OriginalValue,
            ["expectedCompletionDate"] = entry.Property(x => x.ExpectedCompletionDate).OriginalValue,
        });
        var newJson = type == RequirementChangeType.Removed ? null : RequirementFields.ForTask(entry.Entity);
        if (type == RequirementChangeType.Changed && oldJson == newJson) return null;
        return new(projectId, RequirementEntityType.Task, () => entry.Entity.Id, () => entry.Entity.ProjectId,
            type, () => entry.Entity.Title, oldJson, () => newJson);
    }

    private static RequirementCandidate? CandidateForSubTask(EntityEntry<SubTask> entry, int projectId)
    {
        var type = GetChangeType(entry);
        var oldJson = entry.State == EntityState.Added ? null : RequirementFields.Serialize(new Dictionary<string, object?>
        {
            ["title"] = entry.Property(x => x.Title).OriginalValue,
        });
        var newJson = type == RequirementChangeType.Removed ? null : RequirementFields.ForSubTask(entry.Entity);
        if (type == RequirementChangeType.Changed && oldJson == newJson) return null;
        return new(projectId, RequirementEntityType.SubTask, () => entry.Entity.Id, () => entry.Entity.TaskId,
            type, () => entry.Entity.Title, oldJson, () => newJson);
    }

    private static RequirementChangeType GetChangeType<TEntity>(EntityEntry<TEntity> entry) where TEntity : AuditableEntity
    {
        if (entry.State == EntityState.Added) return RequirementChangeType.New;
        if (entry.State == EntityState.Deleted || (!entry.Property(x => x.IsDeleted).OriginalValue && entry.Entity.IsDeleted))
            return RequirementChangeType.Removed;
        return RequirementChangeType.Changed;
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType)) continue;
            var method = typeof(TaskFlowDbContext).GetMethod(nameof(SetSoftDeleteFilter),
                BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entityType.ClrType);
            method.Invoke(null, new object[] { modelBuilder });
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : AuditableEntity =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(x => !x.IsDeleted);

    private sealed record RequirementCandidate(int ProjectId, RequirementEntityType EntityType,
        Func<int> EntityId, Func<int?> ParentEntityId, RequirementChangeType ChangeType,
        Func<string> Title, string? OldValuesJson, Func<string?> NewValuesJson);
    private sealed record PendingRequirementChange(Guid BaselineId, RequirementEntityType EntityType,
        Func<int> EntityId, Func<int?> ParentEntityId, RequirementChangeType ChangeType,
        Func<string> Title, string? OldValuesJson, Func<string?> NewValuesJson);
}
