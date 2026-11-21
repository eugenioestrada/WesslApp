using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class SampleWslcClient : IWslcClient
{
    public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ContainerInfo> containers =
        [
            new("sample-1", "web", "nginx:latest", ContainerState.Running),
            new("sample-2", "database", "postgres:latest", ContainerState.Stopped)
        ];
        return Task.FromResult(containers);
    }
}
