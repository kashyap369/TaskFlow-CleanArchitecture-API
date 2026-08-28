using FluentValidation;

namespace TaskFlow.Application.Features.Planner.Commands.ManagePlannerResource;

public sealed class CreatePlannerNoteCommandValidator : AbstractValidator<CreatePlannerNoteCommand>
{
    public CreatePlannerNoteCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(20000);
    }
}

public sealed class CreatePlannerLinkCommandValidator : AbstractValidator<CreatePlannerLinkCommand>
{
    public CreatePlannerLinkCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048).Must(BeHttpUrl)
            .WithMessage("Link must be an absolute HTTP or HTTPS URL.");
    }

    private static bool BeHttpUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
        parsed.Scheme is "http" or "https";
}

public sealed class UploadPlannerDocumentCommandValidator : AbstractValidator<UploadPlannerDocumentCommand>
{
    public UploadPlannerDocumentCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Length).GreaterThan(0).LessThanOrEqualTo(PlannerResourcePolicy.MaxFileSize);
        RuleFor(x => x.Content).NotNull();
    }
}

public sealed class LinkPlannerResourceCommandValidator : AbstractValidator<LinkPlannerResourceCommand>
{
    public LinkPlannerResourceCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128);
    }
}

public sealed class UpdatePlannerResourceCommandValidator : AbstractValidator<UpdatePlannerResourceCommand>
{
    public UpdatePlannerResourceCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).MaximumLength(20000);
        RuleFor(x => x.Url).MaximumLength(2048);
        RuleFor(x => x.FileName).MaximumLength(255);
    }
}
