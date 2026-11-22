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
    public void ImageCommandsUseTheInstalledWslcArgumentOrder()
    {
        var client = new WslcClient("wslc");

        Assert.Equal(
            ["image", "list", "--format", "json", "--digests"],
            client.CreateImageListStartInfo().ArgumentList);
        Assert.Equal(
            ["image", "pull", "nginx:latest"],
            client.CreateImagePullStartInfo("nginx:latest").ArgumentList);
        Assert.Equal(
            ["image", "tag", "nginx:latest", "local/nginx:dev"],
            client.CreateImageTagStartInfo("nginx:latest", "local/nginx:dev").ArgumentList);
        Assert.Throws<ArgumentException>(() => client.CreateImagePullStartInfo("--help"));
    }

    [Fact]
    public void InspectCommandRequestsBatchedJsonAndPassesIdsAsArguments()
    {
        var client = new WslcClient("wslc");
        var startInfo = client.CreateContainerInspectStartInfo(["container-123", "container-456"]);

        Assert.Equal(
            ["container", "inspect", "--format", "json", "container-123", "container-456"],
            startInfo.ArgumentList);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.Throws<ArgumentException>(() => client.CreateContainerInspectStartInfo(["--help"]));
    }

    [Fact]
    public void ContainerDetailsCommandRequestsSingleContainerJson()
    {
        var client = new WslcClient("wslc");
        var startInfo = client.CreateContainerDetailsStartInfo("container-123");

        Assert.Equal(["container", "inspect", "--format", "json", "container-123"], startInfo.ArgumentList);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.Throws<ArgumentException>(() => client.CreateContainerDetailsStartInfo("--help"));
    }

    [Fact]
    public async Task UnavailableRefreshRetainsTheLastSuccessfulContainersAndRecovers()
    {
        var client = new ChangingClient();
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        Assert.Single(service.Containers);

        client.Failure = new WslcUnavailableException("not found");
        await service.RefreshAsync();
        Assert.True(service.IsWslcUnavailable);
        Assert.True(service.HasRefreshed);
        Assert.Single(service.Containers);
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
        var failures = Assert.IsType<LocalizedMessageList>(result.Error.Arguments[0]);
        var failure = Assert.Single(failures.Messages);
        Assert.Equal("TrayStopAllFailureDetail", failure.ResourceKey);
        Assert.Equal("one", failure.Arguments[0]);
        Assert.Equal("stop failed", failure.Arguments[1]);
    }

    [Fact]
    public void OpeningShellForStoppedContainerReturnsLocalizedError()
    {
        var service = new ContainerService(new ChangingClient(), action => action(), new UnusedShellLauncher());

        service.OpenShell(new("stopped", "stopped", "image", ContainerState.Stopped));

        Assert.Equal("ShellRequiresRunningContainer", service.ShellError!.ResourceKey);
        Assert.Empty(service.ShellError.Arguments);
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
    public async Task BulkStartAndRestartUsePerContainerCallsAndReportEachResult()
    {
        var client = new BulkActionClient(
            new("start-one", "start one", "image", ContainerState.Stopped),
            new("start-two", "start two", "image", ContainerState.Stopped),
            new("restart-one", "restart one", "image", ContainerState.Running),
            new("restart-two", "restart two", "image", ContainerState.Running))
        {
            FailStartId = "start-two",
            FailRestartId = "restart-two"
        };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        var startResult = await service.StartSelectedAsync(
            service.Containers.Where(container => container.State == ContainerState.Stopped).ToArray());
        var restartResult = await service.RestartSelectedAsync(
            service.Containers.Where(container => container.State == ContainerState.Running).ToArray());

        Assert.Equal(["start-one", "start-two"], client.StartAttempts);
        Assert.Equal(["restart-one", "restart-two"], client.RestartAttempts);
        Assert.Equal([1, 2], startResult.Status!.Arguments.Take(2));
        Assert.Equal([1, 2], restartResult.Status!.Arguments.Take(2));
        Assert.Equal("BulkActionItemSucceeded",
            Assert.Single(Assert.IsType<LocalizedMessageList>(startResult.Status.Arguments[2]).Messages).ResourceKey);
        Assert.Equal("start one",
            Assert.Single(Assert.IsType<LocalizedMessageList>(startResult.Status.Arguments[2]).Messages).Arguments[0]);
        Assert.Equal("BulkActionPartialFailure", startResult.Error!.ResourceKey);
        Assert.Equal("start two",
            Assert.Single(Assert.IsType<LocalizedMessageList>(startResult.Error.Arguments[0]).Messages).Arguments[0]);
        Assert.Equal("BulkActionPartialFailure", restartResult.Error!.ResourceKey);
        Assert.Equal("restart two",
            Assert.Single(Assert.IsType<LocalizedMessageList>(restartResult.Error.Arguments[0]).Messages).Arguments[0]);
    }

    [Fact]
    public async Task BulkRemoveRechecksStateAndOnlyRemovesCurrentlyStoppedContainers()
    {
        var client = new BulkActionClient(
            new("still-stopped", "still stopped", "image", ContainerState.Stopped),
            new("started-now", "started now", "image", ContainerState.Running),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped))
        {
            FailRemoveId = "remove-fails"
        };
        client.ListResponses.Enqueue([
            new("still-stopped", "still stopped", "image", ContainerState.Stopped),
            new("started-now", "started now", "image", ContainerState.Stopped),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped)
        ]);
        client.ListResponses.Enqueue([
            new("still-stopped", "still stopped", "image", ContainerState.Stopped),
            new("started-now", "started now", "image", ContainerState.Running),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped)
        ]);
        client.ListResponses.Enqueue([
            new("still-stopped", "still stopped", "image", ContainerState.Stopped),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped)
        ]);
        client.ListResponses.Enqueue([
            new("still-stopped", "still stopped", "image", ContainerState.Stopped),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped)
        ]);
        client.ListResponses.Enqueue([
            new("started-now", "started now", "image", ContainerState.Running),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped)
        ]);
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        var selected = new ContainerInfo[]
        {
            new("still-stopped", "still stopped", "image", ContainerState.Stopped),
            new("started-now", "started now", "image", ContainerState.Stopped),
            new("no-longer-listed", "no longer listed", "image", ContainerState.Stopped),
            new("remove-fails", "remove fails", "image", ContainerState.Stopped)
        };

        var result = await service.RemoveSelectedAsync(selected);

        Assert.Equal(["still-stopped", "remove-fails"], client.RemoveAttempts);
        Assert.Equal([1, 4], result.Status!.Arguments.Take(2));
        var failures = Assert.IsType<LocalizedMessageList>(result.Error!.Arguments[0]).Messages;
        Assert.Equal(3, failures.Count);
        Assert.Contains(failures, failure => failure.ResourceKey == "BulkActionStateChanged"
            && Equals(failure.Arguments[0], "started now"));
        Assert.Contains(failures, failure => failure.ResourceKey == "BulkActionNotFound"
            && Equals(failure.Arguments[0], "no longer listed"));
        Assert.Contains(failures, failure => failure.ResourceKey == "BulkActionFailureDetail"
            && Equals(failure.Arguments[0], "remove fails"));
    }

    [Fact]
    public async Task ConcurrentBulkActionsAreRejectedUntilTheCurrentActionCompletes()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new BulkActionClient(
            new("stopped", "stopped", "image", ContainerState.Stopped),
            new("running", "running", "image", ContainerState.Running))
        {
            StartCompletion = completion.Task
        };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());

        var startTask = service.StartSelectedAsync([client.Containers[0]]);
        Assert.True(service.IsBusy);
        var restartResult = await service.RestartSelectedAsync([client.Containers[1]]);

        Assert.Equal("WaitForCurrentAction", restartResult.Error!.ResourceKey);
        Assert.Empty(client.RestartAttempts);
        completion.SetResult();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(service.IsBusy);
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

        public Task<IReadOnlyDictionary<string, ContainerDiagnostics>> GetContainerDiagnosticsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default) =>
            NoDiagnosticsAsync();

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContainerDetails> GetContainerDetailsAsync(string id, CancellationToken cancellationToken = default) =>
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

        public Task<IReadOnlyDictionary<string, ContainerDiagnostics>> GetContainerDiagnosticsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default) =>
            NoDiagnosticsAsync();

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContainerDetails> GetContainerDetailsAsync(string id, CancellationToken cancellationToken = default) =>
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

    private sealed class BulkActionClient(params ContainerInfo[] containers) : IWslcClient
    {
        public IReadOnlyList<ContainerInfo> Containers { get; } = containers;
        public Queue<IReadOnlyList<ContainerInfo>> ListResponses { get; } = [];
        public List<string> StartAttempts { get; } = [];
        public List<string> RestartAttempts { get; } = [];
        public List<string> RemoveAttempts { get; } = [];
        public string? FailStartId { get; init; }
        public string? FailRestartId { get; init; }
        public string? FailRemoveId { get; init; }
        public Task? StartCompletion { get; init; }

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ListResponses.TryDequeue(out var response) ? response : Containers);

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContainerDetails> GetContainerDetailsAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            StartAttempts.Add(id);
            return FailStartId == id
                ? Task.FromException(new InvalidOperationException("start failed"))
                : StartCompletion ?? Task.CompletedTask;
        }

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            RestartAttempts.Add(id);
            return FailRestartId == id
                ? Task.FromException(new InvalidOperationException("restart failed"))
                : Task.CompletedTask;
        }

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default)
        {
            RemoveAttempts.Add(id);
            return FailRemoveId == id
                ? Task.FromException(new InvalidOperationException("remove failed"))
                : Task.CompletedTask;
        }
    }

    private sealed class UnusedShellLauncher : IContainerShellLauncher
    {
        public void OpenShell(string containerId, ContainerTerminal terminal) => throw new NotSupportedException();
    }

    private static Task<IReadOnlyDictionary<string, ContainerDiagnostics>> NoDiagnosticsAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, ContainerDiagnostics>>(
            new Dictionary<string, ContainerDiagnostics>(StringComparer.Ordinal));

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
