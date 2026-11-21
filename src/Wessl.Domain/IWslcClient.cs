namespace Wessl.Domain;

public interface IWslcClient
{
    Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default);

    Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<string, ContainerUsage>> GetContainerUsagesAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, ContainerUsage>();
        foreach (var id in ids)
        {
            result.Add(id, await GetContainerUsageAsync(id, cancellationToken));
        }
        return result;
    }

    Task StartContainerAsync(string id, CancellationToken cancellationToken = default);

    Task StopContainerAsync(string id, CancellationToken cancellationToken = default);

    Task RestartContainerAsync(string id, CancellationToken cancellationToken = default);

    Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default);
}
