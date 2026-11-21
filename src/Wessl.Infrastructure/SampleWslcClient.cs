using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class SampleWslcClient : IWslcClient
{
    public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ContainerInfo> containers =
        [
            new ContainerInfo("sample-1", "web", "nginx:latest", ContainerState.Running)
            {
                Ports = ["0.0.0.0:8080->80/tcp"]
            },
            new ContainerInfo("sample-2", "database", "postgres:latest", ContainerState.Stopped)
            {
                Ports = ["5432/tcp"]
            }
        ];
        return Task.FromResult(containers);
    }
}
