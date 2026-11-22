namespace Wessl.Domain;

public sealed record ContainerInfo(string Id, string Name, string Image, ContainerState State)
{
    public IReadOnlyList<string> Ports { get; init; } = [];
    public IReadOnlyList<ExposedPort> PublishedPorts { get; init; } = [];
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
    public ContainerDiagnostics Diagnostics { get; init; } = ContainerDiagnostics.Unavailable;
}
