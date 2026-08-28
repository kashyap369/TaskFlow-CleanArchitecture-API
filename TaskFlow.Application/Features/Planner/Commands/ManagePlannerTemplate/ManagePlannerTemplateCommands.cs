using FluentValidation;
using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;

namespace TaskFlow.Application.Features.Planner.Commands.ManagePlannerTemplate;

public sealed record PlannerTemplateDefinition(string Name, PlannerNodeType ObjectType, string Icon, string Header,
    string BackgroundColor, string StrokeColor, int DefaultWidth, int DefaultHeight, string VisibleFieldsJson,
    string DefaultValuesJson, int SortOrder, bool IsActive);
public sealed record CreatePlannerTemplateCommand(PlannerTemplateDefinition Definition) : IRequest<Guid>;
public sealed record UpdatePlannerTemplateCommand(Guid TemplateId, PlannerTemplateDefinition Definition) : IRequest<Guid?>;
public sealed record PublishPlannerTemplateCommand(Guid TemplateId) : IRequest<Guid>;
public sealed record ArchivePlannerTemplateCommand(Guid TemplateId) : IRequest;

internal static class PlannerTemplateAuthorization
{
    public static void EnsureAdmin(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin) throw new ForbiddenException("PLANNER_TEMPLATE_ADMIN_REQUIRED", "Only platform admins can manage Planner templates.");
    }
}

public sealed class CreatePlannerTemplateCommandHandler(IPlannerTemplateRepository templates, IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<CreatePlannerTemplateCommand, Guid>
{
    public async Task<Guid> Handle(CreatePlannerTemplateCommand request, CancellationToken cancellationToken)
    {
        PlannerTemplateAuthorization.EnsureAdmin(currentUser); var d = request.Definition;
        var template = new PlannerTemplate(d.Name, d.ObjectType, d.Icon, d.Header, d.BackgroundColor, d.StrokeColor,
            d.DefaultWidth, d.DefaultHeight, d.VisibleFieldsJson, d.DefaultValuesJson, d.SortOrder, d.IsActive, currentUser.UserId);
        await templates.AddAsync(template, cancellationToken); await unitOfWork.SaveChangesAsync(cancellationToken); return template.Id;
    }
}
public sealed class UpdatePlannerTemplateCommandHandler(IPlannerTemplateRepository templates, IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<UpdatePlannerTemplateCommand, Guid?>
{
    public async Task<Guid?> Handle(UpdatePlannerTemplateCommand request, CancellationToken cancellationToken)
    {
        PlannerTemplateAuthorization.EnsureAdmin(currentUser);
        var template = await templates.GetAsync(request.TemplateId, cancellationToken) ?? throw new NotFoundException("PLANNER_TEMPLATE_NOT_FOUND", "Planner template not found.");
        if (template.ObjectType != request.Definition.ObjectType) throw new ConflictException("PLANNER_TEMPLATE_TYPE_IMMUTABLE", "A template object type cannot be changed.");
        var d = request.Definition; var version = template.Update(d.Name, d.Icon, d.Header, d.BackgroundColor, d.StrokeColor,
            d.DefaultWidth, d.DefaultHeight, d.VisibleFieldsJson, d.DefaultValuesJson, d.SortOrder, d.IsActive, currentUser.UserId);
        if (version is not null) templates.AddVersion(version);
        await unitOfWork.SaveChangesAsync(cancellationToken); return version?.Id;
    }
}
public sealed class PublishPlannerTemplateCommandHandler(IPlannerTemplateRepository templates, IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<PublishPlannerTemplateCommand, Guid>
{
    public async Task<Guid> Handle(PublishPlannerTemplateCommand request, CancellationToken cancellationToken)
    {
        PlannerTemplateAuthorization.EnsureAdmin(currentUser);
        var template = await templates.GetAsync(request.TemplateId, cancellationToken) ?? throw new NotFoundException("PLANNER_TEMPLATE_NOT_FOUND", "Planner template not found.");
        var version = template.Publish(currentUser.UserId); templates.AddVersion(version); await unitOfWork.SaveChangesAsync(cancellationToken); return version.Id;
    }
}
public sealed class ArchivePlannerTemplateCommandHandler(IPlannerTemplateRepository templates, IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<ArchivePlannerTemplateCommand>
{
    public async Task Handle(ArchivePlannerTemplateCommand request, CancellationToken cancellationToken)
    {
        PlannerTemplateAuthorization.EnsureAdmin(currentUser);
        var template = await templates.GetAsync(request.TemplateId, cancellationToken) ?? throw new NotFoundException("PLANNER_TEMPLATE_NOT_FOUND", "Planner template not found.");
        template.Archive(); await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PlannerTemplateDefinitionValidator : AbstractValidator<PlannerTemplateDefinition>
{
    public PlannerTemplateDefinitionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100); RuleFor(x => x.ObjectType).IsInEnum();
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(50); RuleFor(x => x.Header).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DefaultWidth).InclusiveBetween(160, 800); RuleFor(x => x.DefaultHeight).InclusiveBetween(80, 600);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
    }
}
public sealed class CreatePlannerTemplateCommandValidator : AbstractValidator<CreatePlannerTemplateCommand>
{ public CreatePlannerTemplateCommandValidator() => RuleFor(x => x.Definition).SetValidator(new PlannerTemplateDefinitionValidator()); }
public sealed class UpdatePlannerTemplateCommandValidator : AbstractValidator<UpdatePlannerTemplateCommand>
{ public UpdatePlannerTemplateCommandValidator() { RuleFor(x => x.TemplateId).NotEmpty(); RuleFor(x => x.Definition).SetValidator(new PlannerTemplateDefinitionValidator()); } }
