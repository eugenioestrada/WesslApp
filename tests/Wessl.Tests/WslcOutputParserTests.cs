using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class WslcOutputParserTests
{
    [Fact]
    public void ParseReadsCpuAndMemoryUsageFromJsonLines()
    {
        const string output = """
            {"ID":"abc","Name":"web","CPUPerc":"0.16%","MemUsage":"47.21MiB / 15.62GiB"}
            """;

        Assert.Equal(
            new ContainerUsage("0.16%", "47.21MiB / 15.62GiB"),
            WslcOutputParser.ParseUsage(output));
    }

    [Fact]
    public void ParseUsageRejectsEmptyOrIncompleteOutput()
    {
        Assert.Throws<FormatException>(() => WslcOutputParser.ParseUsage(""));
        Assert.Throws<FormatException>(() => WslcOutputParser.ParseUsage("""{"CPUPerc":"0.00%"}"""));
    }

    [Fact]
    public void ParseUsagesReadsFullIdsAndRealStatsFieldsFromJsonLines()
    {
        const string firstId = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        const string secondId = "abcdef012345fedcba9876543210fedcba9876543210fedcba9876543210fedc";
        const string output = """

            {"ID":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","Name":"web","CPUPerc":"0.16%","MemUsage":"47.21MiB / 15.62GiB","MemPerc":"0.30%","NetIO":"1.2kB / 4.5kB","BlockIO":"0B / 12kB","PIDs":"9"}
            {"id":"abcdef012345fedcba9876543210fedcba9876543210fedcba9876543210fedc","cpuperc":"2.50%","memusage":"2GiB / 8GiB","memperc":"25.00%","netio":"5MB / 6MB","blockio":"7MB / 8MB","pids":12}
            """;

        var usages = WslcOutputParser.ParseUsages(output);

        Assert.Equal(2, usages.Count);
        Assert.Equal(new ContainerUsage("0.16%", "47.21MiB / 15.62GiB", "1.2kB / 4.5kB", "0B / 12kB", "9", "0.30%"), usages[firstId]);
        Assert.Equal(new ContainerUsage("2.50%", "2GiB / 8GiB", "5MB / 6MB", "7MB / 8MB", "12", "25.00%"), usages[secondId]);
        Assert.DoesNotContain("abcdef012345", usages.Keys);
    }

    [Fact]
    public void ParseUsagesAllowsEmptyOutputAndOptionalFields()
    {
        Assert.Empty(WslcOutputParser.ParseUsages(" \r\n\n"));
        var usages = WslcOutputParser.ParseUsages("""{"ID":"abc","CPUPerc":"0.00%","MemUsage":"0B / 1GiB"}""");
        Assert.Equal(new ContainerUsage("0.00%", "0B / 1GiB"), usages["abc"]);
    }

    [Theory]
    [InlineData("""{"CPUPerc":"1%","MemUsage":"2MiB"}""")]
    [InlineData("""{"ID":"","CPUPerc":"1%","MemUsage":"2MiB"}""")]
    [InlineData("""{"ID":"abc","MemUsage":"2MiB"}""")]
    [InlineData("""{"ID":"abc","CPUPerc":"1%"}""")]
    [InlineData("""[]""")]
    [InlineData("{\"ID\":\"abc\",\"CPUPerc\":\"1%\",\"MemUsage\":\"2MiB\"}\n{\"ID\":\"abc\",\"CPUPerc\":\"3%\",\"MemUsage\":\"4MiB\"}")]
    public void ParseUsagesRejectsInvalidOrDuplicateEntries(string output)
    {
        Assert.Throws<FormatException>(() => WslcOutputParser.ParseUsages(output));
    }

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
            containers[0].PublishedPorts);
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
            container.PublishedPorts);
    }

    [Fact]
    public void ParseHandlesEmptyOutputAndRejectsMissingIdentity()
    {
        Assert.Empty(WslcOutputParser.Parse(" \n"));
        Assert.Throws<FormatException>(() => WslcOutputParser.Parse("""{"Image":"nginx"}"""));
    }

    [Fact]
    public void ParseReadsPortsAsDelimitedTextOrStringArray()
    {
        const string output = """
            {"ID":"abc","Names":"web","Image":"nginx","State":"running","Ports":"8080:80/tcp, 443/tcp"}
            {"ID":"def","Names":"db","Image":"postgres","State":"stopped","Ports":["5432/tcp","127.0.0.1:15432->5432/tcp"]}
            {"ID":"ghi","Names":"worker","Image":"busybox","State":"running"}
            """;

        var containers = WslcOutputParser.Parse(output);

        Assert.Equal(["8080:80/tcp", "443/tcp"], containers[0].Ports);
        Assert.Equal(["5432/tcp", "127.0.0.1:15432->5432/tcp"], containers[1].Ports);
        Assert.Equal([new ExposedPort(8080, 80, "tcp")], containers[0].PublishedPorts);
        Assert.Equal([new ExposedPort(15432, 5432, "tcp")], containers[1].PublishedPorts);
        Assert.Empty(containers[2].Ports);
    }

    [Fact]
    public async Task SampleClientReturnsContainersAndHonorsCancellation()
    {
        var client = new SampleWslcClient();
        Assert.Equal(2, (await client.ListContainersAsync()).Count);
        Assert.Equal(new ContainerUsage("0.00%", "12.4 MiB / 7.6 GiB", "1.2 kB / 3.4 kB", "0 B / 4 kB", "3", "0.16%"),
            await client.GetContainerUsageAsync("sample-1"));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await client.ListContainersAsync(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.GetContainerUsageAsync("sample-1", source.Token));
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
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetContainerUsageAsync("missing"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetContainerUsageAsync("sample-2"));
    }
}
