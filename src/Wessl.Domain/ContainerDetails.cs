namespace Wessl.Domain;

public sealed record ContainerDetails(
    string? Image,
    string? ImageDigest,
    string? CreatedAt,
    string? StartedAt,
    string? Command,
    IReadOnlyList<string>? Ports,
    IReadOnlyList<string>? Mounts,
    IReadOnlyList<string>? Networks);
