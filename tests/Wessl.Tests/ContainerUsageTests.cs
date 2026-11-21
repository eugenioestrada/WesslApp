using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerUsageTests
{
    [Fact]
    public async Task UsageIsRequestedOnlyOnDemandAndRenderedStateIsUpdated()
    {
        var client = new UsageClient();
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        Assert.Empty(client.Requests);
        Assert.Empty(service.Usage);

        await service.RefreshUsageAsync(Assert.Single(service.Containers));

        Assert.Equal(["container-id"], client.Requests);
        Assert.Equal(new ContainerUsage("1.25%", "25 MiB / 1 GiB"), service.Usage["container-id"]);
        Assert.Empty(service.UsageLoading);
        Assert.Empty(service.UsageErrors);
    }

    [Fact]
    public async Task ConcurrentUsageRequestsForOneContainerAreDeduplicated()
    {
        var completion = new TaskCompletionSource<ContainerUsage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new UsageClient { UsageCompletion = completion.Task };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        var container = Assert.Single(service.Containers);

        var firstRequest = service.RefreshUsageAsync(container);
        var secondRequest = service.RefreshUsageAsync(container);

        Assert.Equal(container.Id, Assert.Single(client.Requests));
        Assert.Contains(container.Id, service.UsageLoading);
        completion.SetResult(new("1.25%", "25 MiB / 1 GiB"));
        await Task.WhenAll(firstRequest, secondRequest);

        Assert.Empty(service.UsageLoading);
        Assert.Equal(new ContainerUsage("1.25%", "25 MiB / 1 GiB"), service.Usage[container.Id]);
    }

    [Fact]
    public async Task FailedRequestIsReportedAndUsageIsClearedWhenContainerStops()
    {
        var client = new UsageClient { UsageFailure = new InvalidOperationException("stats unavailable") };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        var container = Assert.Single(service.Containers);

        await service.RefreshUsageAsync(container);
        Assert.Equal("ContainerUsageError", service.UsageErrors[container.Id].ResourceKey);

        client.UsageFailure = null;
        await service.RefreshUsageAsync(container);
        Assert.Contains(container.Id, service.Usage.Keys);
        client.State = ContainerState.Stopped;
        await service.RefreshAsync();

        Assert.Empty(service.Usage);
        Assert.Empty(service.UsageErrors);
    }

    private sealed class UsageClient : IWslcClient
    {
        public List<string> Requests { get; } = [];
        public ContainerState State { get; set; } = ContainerState.Running;
        public Task<ContainerUsage>? UsageCompletion { get; init; }
        public Exception? UsageFailure { get; set; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContainerInfo>>(
                [new("container-id", "web", "nginx", State)]);

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default)
        {
            Requests.Add(id);
            if (UsageFailure is { } failure)
            {
                return Task.FromException<ContainerUsage>(failure);
            }

            return UsageCompletion ?? Task.FromResult(new ContainerUsage("1.25%", "25 MiB / 1 GiB"));
        }

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedShellLauncher : IContainerShellLauncher
    {
        public void OpenShell(string containerId, ContainerTerminal terminal) => throw new NotSupportedException();
    }
}
