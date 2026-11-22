using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class ContainerListQueryTests
{
    private static readonly ContainerInfo[] Containers =
    [
        new("3", "web", "nginx:latest", ContainerState.Running),
        new("1", "database", "postgres:16", ContainerState.Stopped),
        new("2", "cache", "redis:7", ContainerState.Unknown),
        new("4", "api", "nginx:latest", ContainerState.Stopped)
    ];

    [Theory]
    [InlineData("WEB", "3")]
    [InlineData("POSTGRES", "1")]
    public void SearchMatchesNameAndImageWithoutCaseSensitivity(string searchText, string expectedId)
    {
        var result = ContainerListQuery.Apply(
            Containers,
            searchText,
            null,
            null,
            ContainerSortOrder.Name);

        Assert.Equal(expectedId, Assert.Single(result).Id);
    }

    [Fact]
    public void StateAndImageFiltersCanBeCombined()
    {
        var result = ContainerListQuery.Apply(
            Containers,
            null,
            ContainerState.Stopped,
            "NGINX:LATEST",
            ContainerSortOrder.Name);

        Assert.Equal("4", Assert.Single(result).Id);
    }

    [Fact]
    public void SearchAndFiltersCanProduceNoMatches()
    {
        var result = ContainerListQuery.Apply(
            Containers,
            "missing",
            ContainerState.Running,
            "nginx:latest",
            ContainerSortOrder.Name);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(ContainerSortOrder.Name, "4,2,1,3")]
    [InlineData(ContainerSortOrder.State, "3,4,1,2")]
    [InlineData(ContainerSortOrder.Image, "4,3,1,2")]
    public void SortOrdersAreDeterministic(ContainerSortOrder sortOrder, string expectedIds)
    {
        var result = ContainerListQuery.Apply(Containers, null, null, null, sortOrder);

        Assert.Equal(expectedIds.Split(','), result.Select(container => container.Id));
    }
}
