using Microsoft.Extensions.DependencyInjection;
using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class WslcIntegrationTests
{
    [Fact]
    public void RegistrationSelectsRealClient()
    {
        using var services = new ServiceCollection().AddWslcClient().BuildServiceProvider();
        Assert.IsType<WslcClient>(services.GetRequiredService<IWslcClient>());
    }

    [Fact]
    public async Task MissingExecutableReportsUnavailable()
    {
        var client = new WslcClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "wslc"));
        var error = await Assert.ThrowsAsync<WslcUnavailableException>(() => client.ListContainersAsync());
        Assert.NotNull(error.InnerException);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task UnavailableStateReplacesStaleContainersAndRecovers()
    {
        var client = new ChangingClient();
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        Assert.Single(service.Containers);

        client.Failure = new WslcUnavailableException("not found");
        await service.RefreshAsync();
        Assert.True(service.IsWslcUnavailable);
        Assert.False(service.HasRefreshed);
        Assert.Empty(service.Containers);
        Assert.Equal("not found", service.Error!.Arguments[0]);

        client.Failure = null;
        await service.RefreshAsync();
        Assert.False(service.IsWslcUnavailable);
        Assert.True(service.HasRefreshed);
        Assert.Single(service.Containers);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task CommandFailureRemainsAnErrorRatherThanUnavailableState()
    {
        var client = new ChangingClient { Failure = new InvalidOperationException("exit code 1") };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());

        await service.RefreshAsync();

        Assert.False(service.IsWslcUnavailable);
        Assert.Equal("RefreshError", service.Error!.ResourceKey);
        Assert.Equal("exit code 1", service.Error.Arguments[0]);
    }

    [Fact]
    public async Task StopAllStopsEveryRunningContainerAndSkipsStoppedContainers()
    {
        var client = new BatchClient(
            new("one", "one", "image", ContainerState.Running),
            new("stopped", "stopped", "image", ContainerState.Stopped),
            new("two", "two", "image", ContainerState.Running));
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        var result = await service.StopAllAsync();

        Assert.Equal(["one", "two"], client.StopAttempts);
        Assert.Equal("TrayStopAllCompleted", result.Status!.ResourceKey);
        Assert.Equal([2, 2], result.Status.Arguments);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task StopAllContinuesAfterFailureAndReportsPartialResults()
    {
        var client = new BatchClient(
            new("one", "one", "image", ContainerState.Running),
            new("two", "two", "image", ContainerState.Running),
            new("stopped", "stopped", "image", ContainerState.Stopped))
        {
            FailId = "one"
        };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        var result = await service.StopAllAsync();

        Assert.Equal(["one", "two"], client.StopAttempts);
        Assert.Equal([1, 2], result.Status!.Arguments);
        Assert.Equal("TrayStopAllPartialFailure", result.Error!.ResourceKey);
        Assert.Contains("one: stop failed", (string)result.Error.Arguments[0]!);
    }

    [Fact]
    public async Task StopAllAwaitsSlowClientWithoutBlockingTheCaller()
    {
        var stopCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new BatchClient(new ContainerInfo("one", "one", "image", ContainerState.Running))
        {
            StopCompletion = stopCompletion.Task
        };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        var stopTask = service.StopAllAsync();

        Assert.False(stopTask.IsCompleted);
        stopCompletion.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class ChangingClient : IWslcClient
    {
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Failure is { } failure
                ? Task.FromException<IReadOnlyList<ContainerInfo>>(failure)
                : Task.FromResult<IReadOnlyList<ContainerInfo>>([new("id", "name", "image", ContainerState.Running)]);

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class BatchClient(params ContainerInfo[] containers) : IWslcClient
    {
        public List<string> StopAttempts { get; } = [];
        public string? FailId { get; init; }
        public Task? StopCompletion { get; init; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContainerInfo>>(containers);

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            StopAttempts.Add(id);
            return FailId == id
                ? Task.FromException(new InvalidOperationException("stop failed"))
                : StopCompletion ?? Task.CompletedTask;
        }

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedShellLauncher : IContainerShellLauncher
    {
        public void OpenShell(string containerId) => throw new NotSupportedException();
    }
}
