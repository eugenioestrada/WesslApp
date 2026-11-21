using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class WslcOutputParserTests
{
    [Fact]
    public void ParseReadsJsonLinesAndStates()
    {
        const string output = """
            {"ID":"abc","Names":"web","Image":"nginx","State":"running"}
            {"ID":"def","Names":"db","Image":"postgres","Status":"Exited (0) 2 hours ago"}
            {"ID":"ghi","Names":"other","Image":"busybox","State":"paused"}
            """;

        var containers = WslcOutputParser.Parse(output);

        Assert.Equal(3, containers.Count);
        Assert.Equal(new ContainerInfo("abc", "web", "nginx", ContainerState.Running), containers[0]);
        Assert.Equal(ContainerState.Stopped, containers[1].State);
        Assert.Equal(ContainerState.Unknown, containers[2].State);
    }

    [Fact]
    public void ParseHandlesEmptyOutputAndRejectsMissingIdentity()
    {
        Assert.Empty(WslcOutputParser.Parse(" \n"));
        Assert.Throws<FormatException>(() => WslcOutputParser.Parse("""{"Image":"nginx"}"""));
    }

    [Fact]
    public async Task SampleClientReturnsContainersAndHonorsCancellation()
    {
        var client = new SampleWslcClient();
        Assert.Equal(2, (await client.ListContainersAsync()).Count);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await client.ListContainersAsync(source.Token));
    }

    [Fact]
    public async Task SampleClientSupportsContainerLifecycleActions()
    {
        var client = new SampleWslcClient();

        await client.StopContainerAsync("sample-1");
        Assert.Equal(ContainerState.Stopped, Assert.Single(await client.ListContainersAsync(), c => c.Id == "sample-1").State);

        await client.StartContainerAsync("sample-2");
        Assert.Equal(ContainerState.Running, Assert.Single(await client.ListContainersAsync(), c => c.Id == "sample-2").State);

        await client.RestartContainerAsync("sample-2");
        await client.RemoveContainerAsync("sample-2");
        Assert.DoesNotContain((await client.ListContainersAsync()), container => container.Id == "sample-2");
    }

    [Fact]
    public async Task SampleClientRejectsActionsForUnknownContainers()
    {
        var client = new SampleWslcClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartContainerAsync("missing"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RemoveContainerAsync("missing"));
    }
}
