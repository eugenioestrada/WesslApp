using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerGroupActionTests
{
    [Fact]
    public async Task StartGroupStartsOnlyStoppedContainersAndReportsPartialFailures()
    {
        var client = new GroupActionClient(
            Container("running", ContainerState.Running),
            Container("stopped", ContainerState.Stopped),
            Container("failed", ContainerState.Stopped))
        {
            FailureId = "failed"
        };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        await service.StartGroupAsync("store", service.Containers);

        Assert.Equal(["stopped", "failed"], client.Started);
        Assert.Equal("GroupActionCompleted", service.Status?.ResourceKey);
        Assert.Equal("GroupActionPartialFailure", service.Error?.ResourceKey);
        Assert.Equal(ContainerState.Running, Assert.Single(service.Containers, container => container.Id == "stopped").State);
    }

    [Fact]
    public async Task StopGroupStopsOnlyRunningContainers()
    {
        var client = new GroupActionClient(
            Container("running", ContainerState.Running),
            Container("stopped", ContainerState.Stopped));
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        await service.StopGroupAsync("store", service.Containers);

        Assert.Equal(["running"], client.Stopped);
        Assert.Equal("GroupActionCompleted", service.Status?.ResourceKey);
        Assert.Null(service.Error);
        Assert.Equal(ContainerState.Stopped, Assert.Single(service.Containers, container => container.Id == "running").State);
    }

    private static ContainerInfo Container(string id, ContainerState state) =>
        new(id, id, "image", state);

    private sealed class GroupActionClient(params ContainerInfo[] containers) : IWslcClient
    {
        private readonly Dictionary<string, ContainerInfo> _containers =
            containers.ToDictionary(container => container.Id);

        public List<string> Started { get; } = [];
        public List<string> Stopped { get; } = [];
        public string? FailureId { get; init; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContainerInfo>>(_containers.Values.ToArray());

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContainerDetails> GetContainerDetailsAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            Started.Add(id);
            return ChangeStateAsync(id, ContainerState.Running);
        }

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            Stopped.Add(id);
            return ChangeStateAsync(id, ContainerState.Stopped);
        }

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private Task ChangeStateAsync(string id, ContainerState state)
        {
            if (id == FailureId)
            {
                return Task.FromException(new InvalidOperationException("operation failed"));
            }

            _containers[id] = _containers[id] with { State = state };
            return Task.CompletedTask;
        }
    }

    private sealed class UnusedShellLauncher : IContainerShellLauncher
    {
        public void OpenShell(string containerId, ContainerTerminal terminal) =>
            throw new NotSupportedException();
    }
}
