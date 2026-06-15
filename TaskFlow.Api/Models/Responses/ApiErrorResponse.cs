namespace TaskFlow.Api.Models.Responses
{
    public sealed class ApiErrorResponse
    {
        public bool Success => false;

        public string Code { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public string FailureReason { get; init; } = string.Empty;

        public object? Errors { get; init; }

        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public string? TraceId { get; init; }
    }
}
