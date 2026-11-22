using Wessl.Domain;

namespace Wessl.App.Services;

public enum ContainerSortOrder
{
    Name,
    State,
    Image
}

public static class ContainerListQuery
{
    public static int CountHiddenSelected(
        IReadOnlyCollection<ContainerInfo> selectedContainers,
        IEnumerable<ContainerInfo> visibleContainers)
    {
        var visibleIds = visibleContainers
            .Select(container => container.Id)
            .ToHashSet(StringComparer.Ordinal);
        return selectedContainers.Count(container => !visibleIds.Contains(container.Id));
    }

    public static IReadOnlyList<ContainerInfo> Apply(
        IEnumerable<ContainerInfo> containers,
        string? searchText,
        ContainerState? state,
        string? image,
        ContainerSortOrder sortOrder)
    {
        var filtered = containers.Where(container =>
            (string.IsNullOrWhiteSpace(searchText)
                || container.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || container.Image.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            && (state is null || container.State == state)
            && (image is null || string.Equals(container.Image, image, StringComparison.OrdinalIgnoreCase)));

        return sortOrder switch
        {
            ContainerSortOrder.State => filtered
                .OrderBy(container => StateSortKey(container.State))
                .ThenBy(container => container.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(container => container.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ContainerSortOrder.Image => filtered
                .OrderBy(container => container.Image, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(container => container.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(container => container.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => filtered
                .OrderBy(container => container.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(container => container.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static int StateSortKey(ContainerState state) => state switch
    {
        ContainerState.Running => 0,
        ContainerState.Stopped => 1,
        _ => 2
    };
}
