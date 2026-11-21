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
