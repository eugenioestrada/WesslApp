namespace Wessl.Domain;

public enum ContainerHealthStatus
{
    Unknown,
    Unavailable,
    Starting,
    Healthy,
    Unhealthy
}

public sealed record ContainerDiagnostics(
    ContainerHealthStatus Health,
    int? ExitCode,
    DateTimeOffset? FinishedAt,
    string? LatestHealthCheckError)
{
    public static ContainerDiagnostics Unavailable { get; } =
        new(ContainerHealthStatus.Unavailable, null, null, null);
}
