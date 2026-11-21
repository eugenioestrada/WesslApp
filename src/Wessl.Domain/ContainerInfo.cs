namespace Wessl.Domain;

public sealed record ContainerInfo(string Id, string Name, string Image, ContainerState State)
{
    public IReadOnlyList<ExposedPort> Ports { get; init; } = Array.Empty<ExposedPort>();
}
