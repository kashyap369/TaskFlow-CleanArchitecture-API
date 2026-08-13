namespace TaskFlow.Infra.Security
{
    public sealed class OneTimeCodeSettings
    {
        public string SecretKey { get; init; } = string.Empty;
    }
}
