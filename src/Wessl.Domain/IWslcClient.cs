namespace Wessl.Domain;

public interface IWslcClient
{
    Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default);
}
