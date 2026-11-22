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

        public Task<IReadOnlyDictionary<string, ContainerDiagnostics>> GetContainerDiagnosticsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default) =>
            NoDiagnosticsAsync();

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

    private static Task<IReadOnlyDictionary<string, ContainerDiagnostics>> NoDiagnosticsAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, ContainerDiagnostics>>(
            new Dictionary<string, ContainerDiagnostics>(StringComparer.Ordinal));

    [Fact]
    public async Task AutomaticUsageRequestsOnlyRunningContainersAndSkipsEmptyOrCancelledRefreshes()
    {
        var client = new AutomaticUsageClient();
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await service.RefreshUsagesAsync(cancelled.Token);
        Assert.Empty(client.Requests);
        await service.RefreshUsagesAsync(CancellationToken.None);

        Assert.Equal(new[] { "web", "db" }, Assert.Single(client.Requests));
        Assert.Equal(new ContainerUsage("12%", "128 MiB", "1 MB / 2 MB"), service.Usage["web"]);
        Assert.Equal(new ContainerUsage("3%", "256 MiB"), service.Usage["db"]);
        Assert.DoesNotContain("stopped", service.Usage.Keys);
        Assert.Empty(service.UsageLoading);
        Assert.Empty(service.UsageErrors);

        client.Containers = [new("stopped", "old", "image", ContainerState.Stopped)];
        await service.RefreshAsync();
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Single(client.Requests);
        Assert.Empty(service.Usage);
    }

    [Fact]
    public async Task AutomaticUsageDoesNotOverlapAndCanRunAgainAfterCompletion()
    {
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, ContainerUsage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AutomaticUsageClient { NextResult = pending.Task };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();

        var first = service.RefreshUsagesAsync(CancellationToken.None);
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Single(client.Requests);
        Assert.Equal(new[] { "db", "web" }, service.UsageLoading.OrderBy(id => id).ToArray());

        pending.SetResult(AutomaticUsageClient.Usages());
        await first;
        Assert.Empty(service.UsageLoading);
        client.NextResult = null;
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal("12%", service.Usage["web"].CpuPercent);
    }

    [Fact]
    public async Task AutomaticUsageReportsErrorsAndMissingDataThenRecovers()
    {
        var client = new AutomaticUsageClient();
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        await service.RefreshUsagesAsync(CancellationToken.None);

        client.NextResult = Task.FromException<IReadOnlyDictionary<string, ContainerUsage>>(
            new InvalidOperationException("stats failed"));
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Equal("ContainerUsageError", service.UsageErrors["web"].ResourceKey);
        Assert.Equal("stats failed", Assert.Single(service.UsageErrors["db"].Arguments));
        Assert.Equal("12%", service.Usage["web"].CpuPercent);
        Assert.Empty(service.UsageLoading);

        client.NextResult = Task.FromResult<IReadOnlyDictionary<string, ContainerUsage>>(
            new Dictionary<string, ContainerUsage> { ["web"] = new("20%", "512 MiB") });
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Equal("20%", service.Usage["web"].CpuPercent);
        Assert.DoesNotContain("web", service.UsageErrors.Keys);
        Assert.DoesNotContain("db", service.Usage.Keys);
        Assert.Equal("ContainerUsageUnavailable", service.UsageErrors["db"].ResourceKey);

        client.NextResult = null;
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Empty(service.UsageErrors);
        Assert.Equal("3%", service.Usage["db"].CpuPercent);
        Assert.Equal(4, client.Requests.Count);
    }

    [Fact]
    public async Task AutomaticUsageDoesNotApplyResultsForContainersThatStoppedInFlight()
    {
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, ContainerUsage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AutomaticUsageClient { NextResult = pending.Task };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        var request = service.RefreshUsagesAsync(CancellationToken.None);

        client.Containers = [
            new("web", "frontend", "image", ContainerState.Stopped),
            new("db", "database", "image", ContainerState.Running)
        ];
        await service.RefreshAsync();
        pending.SetResult(AutomaticUsageClient.Usages());
        await request;

        Assert.DoesNotContain("web", service.Usage.Keys);
        Assert.Equal("3%", service.Usage["db"].CpuPercent);
        Assert.Empty(service.UsageLoading);
        Assert.Empty(service.UsageErrors);
    }

    [Fact]
    public async Task CancelledAutomaticRequestDoesNotReportErrorAndCanRecover()
    {
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, ContainerUsage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AutomaticUsageClient { NextResult = pending.Task };
        var service = new ContainerService(client, action => action(), new UnusedShellLauncher());
        await service.RefreshAsync();
        using var source = new CancellationTokenSource();

        var request = service.RefreshUsagesAsync(source.Token);
        Assert.Equal(2, service.UsageLoading.Count);
        source.Cancel();
        service.StopUsagePolling();
        pending.SetCanceled(source.Token);
        await request;
        Assert.Empty(service.UsageLoading);
        Assert.Empty(service.UsageErrors);
        Assert.Empty(service.Usage);

        client.NextResult = null;
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal("12%", service.Usage["web"].CpuPercent);
        Assert.Empty(service.UsageErrors);
    }

    [Fact]
    public async Task StopUsagePollingClearsStateAndRejectsAlreadyDispatchedResult()
    {
        var client = new AutomaticUsageClient();
        var updates = new List<Action>();
        var queue = false;
        var service = new ContainerService(client, action =>
        {
            if (queue) updates.Add(action);
            else action();
        }, new UnusedShellLauncher());
        await service.RefreshAsync();
        await service.RefreshUsagesAsync(CancellationToken.None);
        client.NextResult = Task.FromResult<IReadOnlyDictionary<string, ContainerUsage>>(
            new Dictionary<string, ContainerUsage> { ["web"] = new("20%", "512 MiB") });
        await service.RefreshUsagesAsync(CancellationToken.None);
        client.NextResult = Task.FromResult<IReadOnlyDictionary<string, ContainerUsage>>(
            new Dictionary<string, ContainerUsage> { ["web"] = new("90%", "900 MiB") });
        queue = true;
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Single(updates);
        Assert.Equal("20%", service.Usage["web"].CpuPercent);
        Assert.Equal("ContainerUsageUnavailable", service.UsageErrors["db"].ResourceKey);
        Assert.Contains("db", service.UsageLoading);

        service.StopUsagePolling();
        Assert.Empty(service.Usage);
        Assert.Empty(service.UsageErrors);
        Assert.Empty(service.UsageLoading);
        updates[0]();
        Assert.Empty(service.Usage);
        Assert.Empty(service.UsageErrors);

        queue = false;
        client.NextResult = null;
        await service.RefreshUsagesAsync(CancellationToken.None);
        Assert.Equal("12%", service.Usage["web"].CpuPercent);
        Assert.Empty(service.UsageErrors);
    }

    [Fact]
    public async Task TickStatsCancelsOnHideDiscardsLateResultAndRecoversAfterReopening()
    {
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, ContainerUsage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatched = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AutomaticUsageClient { NextResult = pending.Task };
        var queue = false;
        var service = new ContainerService(client, action =>
        {
            if (queue) dispatched.SetResult(action);
            else action();
        }, new UnusedShellLauncher());
        var polling = new ContainerPolling(service);

        polling.TickStats();
        Assert.Empty(client.Requests);
        polling.Start();
        polling.TickStats();
        Assert.Single(client.Requests);
        Assert.Equal(2, service.UsageLoading.Count);
        var oldToken = client.Tokens[0];
        polling.Stop();
        Assert.True(oldToken.IsCancellationRequested);
        Assert.Empty(service.UsageLoading);
        Assert.Empty(service.Usage);
        Assert.Empty(service.UsageErrors);
        polling.TickStats();
        polling.Start();
        polling.TickStats();
        Assert.Single(client.Requests);

        queue = true;
        pending.SetResult(new Dictionary<string, ContainerUsage> { ["web"] = new("90%", "900 MiB") });
        var lateApply = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queue = false;
        client.NextResult = null;
        polling.TickStats();
        Assert.Equal(2, client.Requests.Count);
        Assert.False(client.Tokens[1].IsCancellationRequested);
        Assert.Equal("12%", service.Usage["web"].CpuPercent);
        lateApply();
        Assert.Equal("12%", service.Usage["web"].CpuPercent);
        Assert.Equal("3%", service.Usage["db"].CpuPercent);
        polling.Stop();
        Assert.True(client.Tokens[1].IsCancellationRequested);
        Assert.Empty(service.Usage);
    }

    private sealed class AutomaticUsageClient : IWslcClient
    {
        public IReadOnlyList<ContainerInfo> Containers { get; set; } = [
            new("web", "frontend", "image", ContainerState.Running),
            new("stopped", "old", "image", ContainerState.Stopped),
            new("db", "database", "image", ContainerState.Running)
        ];
        public List<string[]> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public Task<IReadOnlyDictionary<string, ContainerUsage>>? NextResult { get; set; }

        public static IReadOnlyDictionary<string, ContainerUsage> Usages() =>
            new Dictionary<string, ContainerUsage>
            {
                ["web"] = new("12%", "128 MiB", "1 MB / 2 MB"),
                ["db"] = new("3%", "256 MiB")
            };

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Containers);

        public Task<IReadOnlyDictionary<string, ContainerDiagnostics>> GetContainerDiagnosticsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default) =>
            NoDiagnosticsAsync();

        public Task<IReadOnlyDictionary<string, ContainerUsage>> GetContainerUsagesAsync(
            IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            Requests.Add(ids.ToArray());
            Tokens.Add(cancellationToken);
            return NextResult ?? Task.FromResult(Usages());
        }

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
