using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Api.Models.Requests;

public sealed record LinkPlannerProjectRequest(string ElementId, Guid? TemplateVersionId = null);
public sealed record CreatePlannerTaskNodeRequest(string ElementId, string Title, string Description,
    DateTime StartDate, TaskPriority Priority, DateTime? ExpectedCompletionDate, Guid? TemplateVersionId = null,
    string? ChangeReason = null);
public sealed record CreatePlannerSubTaskNodeRequest(string ElementId, int TaskId, string Title,
    Guid? TemplateVersionId = null, string? ChangeReason = null);
public sealed record UpdatePlannerNodeRequest(string Title, string? Description, DateTime? ExpectedCompletionDate,
    TaskPriority? Priority, string? ProblemStatement, decimal? BudgetAmount, string? BudgetCurrency,
    int? ApproximateDurationWeeks, string? ChangeReason = null);
public sealed record CreatePlannerNoteRequest(string ElementId, string Title, string Content,
    Guid? TemplateVersionId = null);
public sealed record CreatePlannerLinkRequest(string ElementId, string Title, string Url,
    Guid? TemplateVersionId = null);
public sealed record LinkPlannerResourceRequest(string ElementId, Guid? TemplateVersionId = null);
public sealed record UpdatePlannerResourceRequest(string Title, string? Content, string? Url,
    string? FileName);

public sealed class UploadPlannerDocumentRequest
{
    public string ElementId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Guid? TemplateVersionId { get; init; }
    public IFormFile File { get; init; } = null!;
}
