namespace Wessl.Domain;

public interface IContainerLogsClient
{
    IAsyncEnumerable<string> FollowLogsAsync(string id, CancellationToken cancellationToken = default);
}
