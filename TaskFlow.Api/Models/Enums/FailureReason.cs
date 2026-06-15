namespace TaskFlow.Api.Models.Enums
{
    public enum FailureReason
    {
        ValidationFailure = 1,
        BusinessRuleViolation = 2,
        Unauthorized = 3,
        Forbidden = 4,
        NotFound = 5,
        Conflict = 6,
        InternalServerError = 7
    }
}
