using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerPollingTests
{
    [Fact]
    public async Task PollsOnlyDuringVisibleSessionsOrManualRefresh()
    {
        var client = new RecordingClient();
        var polling = new ContainerPolling(new ContainerService(client, action => action()));

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
        var service = new ContainerService(client, action => update.SetResult(action));
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
        var service = new ContainerService(new RecordingClient(), updates.Add);
        var polling = new ContainerPolling(service);

        polling.Start();
        Assert.Single(updates);
        polling.Stop();
        updates[0]();

        Assert.Empty(service.Containers);
        await polling.RefreshAsync();
        updates[1]();
        Assert.Single(service.Containers);
    }

    [Fact]
    public async Task ReopeningWaitsForCancelledRefreshThenStartsANewOne()
    {
        var client = new ReopeningClient();
        var polling = new ContainerPolling(new ContainerService(client, action => action()));

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

        public void CompleteFirst() => _first.SetResult([]);
    }
}
