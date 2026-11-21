using Microsoft.Extensions.DependencyInjection;
using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class WslcIntegrationTests
{
    [Fact]
    public void RegistrationUsesRealClientByDefault()
    {
        using var services = new ServiceCollection().AddWslcClient().BuildServiceProvider();
        var client = Assert.IsType<WslcClient>(services.GetRequiredService<IWslcClient>());
        Assert.Same(client, services.GetRequiredService<IImageClient>());
    }

    [Fact]
    public void RegistrationUsesSampleClientOnlyWhenExplicitlyEnabled()
    {
        using var services = new ServiceCollection().AddWslcClient(useSampleClient: true).BuildServiceProvider();
        var client = Assert.IsType<SampleWslcClient>(services.GetRequiredService<IWslcClient>());
        Assert.Same(client, services.GetRequiredService<IImageClient>());
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
    public void StatsCommandRequestsJsonForOneContainer()
    {
        var client = new WslcClient("wslc");
        var startInfo = client.CreateStatsStartInfo("container-123");

        Assert.Equal("wslc", startInfo.FileName);
        Assert.Equal(["stats", "--format", "json", "container-123"], startInfo.ArgumentList);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.Throws<ArgumentException>(() => client.CreateStatsStartInfo("--help"));
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

    [Fact]
    public async Task OpeningShellUsesTheSavedDefaultTerminal()
    {
        var client = new ChangingClient();
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));
        settings.SetDefaultTerminal(ContainerTerminal.PowerShell);
        var launcher = new RecordingShellLauncher();
        var service = new ContainerService(client, action => action(), launcher, settings);

        await service.RefreshAsync();
        service.OpenShell(service.Containers[0]);

        Assert.Equal(ContainerTerminal.PowerShell, launcher.Terminal);
        Assert.Null(service.ShellError);
    }

    [Fact]
    public async Task OpeningShellDefaultsToWindowsTerminalWithoutSavedPreference()
    {
        var client = new ChangingClient();
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));
        var launcher = new RecordingShellLauncher();
        var service = new ContainerService(client, action => action(), launcher, settings);

        await service.RefreshAsync();
        service.OpenShell(service.Containers[0]);

        Assert.Equal(ContainerTerminal.WindowsTerminal, launcher.Terminal);
        Assert.Null(service.ShellError);
    }

    private sealed class ChangingClient : IWslcClient
    {
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Failure is { } failure
                ? Task.FromException<IReadOnlyList<ContainerInfo>>(failure)
                : Task.FromResult<IReadOnlyList<ContainerInfo>>([new("id", "name", "image", ContainerState.Running)]);

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
        public void OpenShell(string containerId, ContainerTerminal terminal) => throw new NotSupportedException();
    }

    private sealed class RecordingShellLauncher : IContainerShellLauncher
    {
        public ContainerTerminal? Terminal { get; private set; }

        public void OpenShell(string containerId, ContainerTerminal terminal) => Terminal = terminal;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "WesslIntegrationTests",
            Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
