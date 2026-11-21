using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class WslcOutputParserTests
{
    [Fact]
    public void ParseReadsJsonLinesAndStates()
    {
        const string output = """
            {"ID":"abc","Names":"web","Image":"nginx","State":"running","Ports":"0.0.0.0:8080->80/tcp, :::8080->80/tcp, 0.0.0.0:5353->53/udp, 80/tcp"}
            {"ID":"def","Names":"db","Image":"postgres","Status":"Exited (0) 2 hours ago"}
            {"ID":"ghi","Names":"other","Image":"busybox","State":"paused"}
            """;

        var containers = WslcOutputParser.Parse(output);

        Assert.Equal(3, containers.Count);
        Assert.Equal("abc", containers[0].Id);
        Assert.Equal("web", containers[0].Name);
        Assert.Equal("nginx", containers[0].Image);
        Assert.Equal(ContainerState.Running, containers[0].State);
        Assert.Equal(
            [new ExposedPort(8080, 80, "tcp"), new ExposedPort(5353, 53, "udp")],
            containers[0].Ports);
        Assert.Equal(ContainerState.Stopped, containers[1].State);
        Assert.Equal(ContainerState.Unknown, containers[2].State);
    }

    [Fact]
    public void ParseReadsPublishedPortsFromStructuredPortData()
    {
        const string output = """{"ID":"abc","Names":"web","Image":"nginx","State":"running","Ports":[{"HostPort":"8081","ContainerPort":"80","Protocol":"tcp"},{"PublicPort":5353,"PrivatePort":53,"Type":"udp"}]}""";

        var container = Assert.Single(WslcOutputParser.Parse(output));

        Assert.Equal(
            [new ExposedPort(8081, 80, "tcp"), new ExposedPort(5353, 53, "udp")],
            container.Ports);
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
}
