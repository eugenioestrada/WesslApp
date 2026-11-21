namespace Wessl.Domain;

public interface IWslcClient
{
    Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default);

    Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default);

    Task StartContainerAsync(string id, CancellationToken cancellationToken = default);

    Task StopContainerAsync(string id, CancellationToken cancellationToken = default);

    Task RestartContainerAsync(string id, CancellationToken cancellationToken = default);

    Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default);
}
