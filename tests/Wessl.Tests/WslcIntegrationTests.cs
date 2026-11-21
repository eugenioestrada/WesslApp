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
        public void OpenShell(string containerId) => throw new NotSupportedException();
    }
}
