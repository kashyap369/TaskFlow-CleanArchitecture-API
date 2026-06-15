namespace TaskFlow.Api.Models.Responses
{
    public sealed class ApiResponse<T>
    {
        public bool Success => true;

        public string Message { get; init; } = string.Empty;

        public T? Data { get; init; }

        public DateTime Timestamp { get; init; }
            = DateTime.UtcNow;
    }
}