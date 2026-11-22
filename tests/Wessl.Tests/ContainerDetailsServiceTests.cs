using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerDetailsServiceTests
{
    [Fact]
    public async Task InspectLoadsDetailsOnlyForTheRequestedContainer()
    {
        var client = new DetailsClient();
        var details = new ContainerDetails(
            "registry.example/api:1.2",
            "sha256:image",
            "created",
            "started",
            "[\"dotnet\",\"service.dll\"]",
            [],
            [],
            []);
        client.Inspect = id =>
        {
            Assert.Equal("container-123", id);
            return Task.FromResult(details);
        };
        var service = new ContainerDetailsService(client, action => action());
        var container = new ContainerInfo("container-123", "api", "registry.example/api:1.2", ContainerState.Running);

        await service.InspectAsync(container);

        Assert.Equal(container, service.Container);
        Assert.Equal(details, service.Details);
        Assert.False(service.IsLoading);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task InspectReportsErrorsAndClearDiscardsSelectedDetails()
    {
        var client = new DetailsClient
        {
            Inspect = _ => Task.FromException<ContainerDetails>(new InvalidOperationException("inspect failed"))
        };
        var service = new ContainerDetailsService(client, action => action());
        var container = new ContainerInfo("container-123", "api", "image", ContainerState.Stopped);

        await service.InspectAsync(container);

        Assert.Null(service.Details);
        Assert.Equal("inspect failed", service.Error?.Message);
        Assert.False(service.IsLoading);

        service.Clear();

        Assert.Null(service.Container);
        Assert.Null(service.Details);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task InspectIgnoresResultsForAContainerThatIsNoLongerSelected()
    {
        var firstRequest = new TaskCompletionSource<ContainerDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new DetailsClient
        {
            Inspect = id => id == "first"
                ? firstRequest.Task
                : Task.FromResult(new ContainerDetails("second-image", null, null, null, null, null, null, null))
        };
        var service = new ContainerDetailsService(client, action => action());
        var first = new ContainerInfo("first", "first", "first-image", ContainerState.Running);
        var second = new ContainerInfo("second", "second", "second-image", ContainerState.Running);

        var firstInspection = service.InspectAsync(first);
        await service.InspectAsync(second);
        firstRequest.SetResult(new ContainerDetails("first-image", null, null, null, null, null, null, null));
        await firstInspection;

        Assert.Equal("second", service.Container?.Id);
        Assert.Equal("second-image", service.Details?.Image);
    }

    private sealed class DetailsClient : IWslcClient
    {
        public Func<string, Task<ContainerDetails>> Inspect { get; set; } =
            _ => throw new NotSupportedException();

        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContainerUsage> GetContainerUsageAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContainerDetails> GetContainerDetailsAsync(string id, CancellationToken cancellationToken = default) =>
            Inspect(id);

        public Task StartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RestartContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
