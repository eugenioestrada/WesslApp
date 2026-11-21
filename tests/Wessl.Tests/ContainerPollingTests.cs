using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerPollingTests
{
    [Fact]
    public async Task PollsOnlyDuringVisibleSessionsOrManualRefresh()
    {
        var client = new RecordingClient();
        var polling = new ContainerPolling(new ContainerService(client, action => action(), new UnusedShellLauncher()));

        polling.Tick();
        Assert.Equal(0, client.Calls);

        polling.Start();
        polling.Start();
        polling.Tick();
        Assert.Equal(2, client.Calls);

        polling.Stop();
        polling.Tick();
        Assert.Equal(2, client.Calls);

        await polling.RefreshAsync();
        Assert.Equal(3, client.Calls);

        polling.Start();
        Assert.Equal(4, client.Calls);
        polling.Stop();
    }

    [Fact]
    public async Task StopCancelsInFlightRefreshAndSuppressesLateResult()
    {
        var client = new BlockingClient();
        var update = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ContainerService(client, action => update.SetResult(action), new UnusedShellLauncher());
        var polling = new ContainerPolling(service);

        polling.Start();
        polling.Tick();
        Assert.Equal(1, client.Calls);
        polling.Stop();
        Assert.True(client.Token.IsCancellationRequested);

        client.Complete();
        var apply = await update.Task.WaitAsync(TimeSpan.FromSeconds(5));
        apply();

        Assert.Empty(service.Containers);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task StopSuppressesAlreadyDispatchedResult()
    {
        var updates = new List<Action>();
        var service = new ContainerService(new RecordingClient(), updates.Add, new UnusedShellLauncher());
        var polling = new ContainerPolling(service);

        Assert.False(service.HasRefreshed);
        polling.Start();
        Assert.Single(updates);
        polling.Stop();
        updates[0]();

        Assert.Empty(service.Containers);
        Assert.False(service.HasRefreshed);
        await polling.RefreshAsync();
        updates[1]();
        Assert.Single(service.Containers);
        Assert.True(service.HasRefreshed);
    }

    [Fact]
    public async Task ReopeningWaitsForCancelledRefreshThenStartsANewOne()
    {
        var client = new ReopeningClient();
        var polling = new ContainerPolling(new ContainerService(client, action => action(), new UnusedShellLauncher()));

        polling.Start();
        polling.Stop();
        polling.Start();
        Assert.Equal(1, client.Calls);

        client.CompleteFirst();
        await client.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, client.Calls);
        polling.Stop();
    }

    private sealed class RecordingClient : IWslcClient
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<ContainerInfo>>(
                [new("id", "name", "image", ContainerState.Running)]);
        }

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedShellLauncher : IContainerShellLauncher
    {
        public void OpenShell(string containerId) =>
            throw new InvalidOperationException("Shell launch is not expected in polling tests.");
    }

    private sealed class BlockingClient : IWslcClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<ContainerInfo>> _result = new();
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            return _result.Task;
        }

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Complete() => _result.SetResult(
            [new("id", "name", "image", ContainerState.Running)]);
    }

    private sealed class ReopeningClient : IWslcClient
    {
        private readonly TaskCompletionSource<IReadOnlyList<ContainerInfo>> _first = new();
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls == 1)
            {
                return _first.Task;
            }

            SecondStarted.SetResult();
            return Task.FromResult<IReadOnlyList<ContainerInfo>>([]);
        }

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void CompleteFirst() => _first.SetResult([]);
    }
}
