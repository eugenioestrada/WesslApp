using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerGroupingTests
{
    [Fact]
    public void GroupsByActualLabelValueAndLeavesMissingMetadataUngrouped()
    {
        var containers = new[]
        {
            WithLabel("one", "store"),
            WithLabel("two", "store"),
            WithLabel("three", "blog"),
            new ContainerInfo("four", "unlabeled", "image", ContainerState.Stopped),
            WithLabel("five", "")
        };

        var result = ContainerGrouping.GroupByLabel(containers, ContainerGrouping.ComposeProjectLabel);

        Assert.Equal(["blog", "store"], result.Groups.Select(group => group.Value));
        Assert.Equal(["one", "two"], result.Groups[1].Containers.Select(container => container.Id));
        Assert.Equal(["four", "five"], result.Ungrouped.Select(container => container.Id));
    }

    [Fact]
    public void DoesNotInventGroupsWhenNoLabelMetadataExists()
    {
        var containers = new[]
        {
            new ContainerInfo("one", "api", "image", ContainerState.Running),
            new ContainerInfo("two", "db", "image", ContainerState.Stopped)
        };

        var result = ContainerGrouping.GroupByLabel(containers, ContainerGrouping.ComposeProjectLabel);

        Assert.Empty(result.Groups);
        Assert.Equal(containers, result.Ungrouped);
        Assert.Empty(ContainerGrouping.GetLabelKeys(containers));
    }

    [Fact]
    public void ListsOnlyLabelKeysPresentInContainerMetadata()
    {
        var containers = new[]
        {
            WithLabel("one", "store"),
            new ContainerInfo("two", "plain", "image", ContainerState.Stopped)
        };

        Assert.Equal(
            [ContainerGrouping.ComposeProjectLabel],
            ContainerGrouping.GetLabelKeys(containers));
    }

    [Fact]
    public void CanGroupByAnArbitraryLabelReturnedByWslc()
    {
        var containers = new[]
        {
            WithLabel("one", "com.example.tier", "frontend"),
            WithLabel("two", "com.example.tier", "backend")
        };

        var result = ContainerGrouping.GroupByLabel(containers, "com.example.tier");

        Assert.Equal(["backend", "frontend"], result.Groups.Select(group => group.Value));
        Assert.Empty(result.Ungrouped);
    }

    private static ContainerInfo WithLabel(string id, string project) =>
        WithLabel(id, ContainerGrouping.ComposeProjectLabel, project);

    private static ContainerInfo WithLabel(string id, string label, string value) =>
        new ContainerInfo(id, id, "image", ContainerState.Running)
        {
            Labels = new Dictionary<string, string> { [label] = value }
        };
}
