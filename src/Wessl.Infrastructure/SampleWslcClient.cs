using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class SampleWslcClient : IWslcClient, IImageClient
{
    public Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ImageInfo>>([]);
    }

    public Task<string> PruneImagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("0 B");
    }

    private readonly List<ContainerInfo> _containers =
    [
        new("sample-1", "web", "nginx:latest", ContainerState.Running),
        new("sample-2", "database", "postgres:latest", ContainerState.Stopped)
    ];

    public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ContainerInfo>>(_containers.ToArray());
    }

    public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
        SetStateAsync(id, ContainerState.Running, cancellationToken);

    public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
        SetStateAsync(id, ContainerState.Stopped, cancellationToken);

    public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
        SetStateAsync(id, ContainerState.Running, cancellationToken);

    public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _containers.Remove(FindContainer(id));
        return Task.CompletedTask;
    }

    private Task SetStateAsync(string id, ContainerState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = _containers.FindIndex(container => container.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Container '{id}' was not found.");
        }

        _containers[index] = _containers[index] with { State = state };
        return Task.CompletedTask;
    }

    private ContainerInfo FindContainer(string id) =>
        _containers.Find(container => container.Id == id)
        ?? throw new InvalidOperationException($"Container '{id}' was not found.");
}
