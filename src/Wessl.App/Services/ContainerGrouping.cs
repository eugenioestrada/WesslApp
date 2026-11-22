using Wessl.Domain;

namespace Wessl.App.Services;

public static class ContainerGrouping
{
    public const string ComposeProjectLabel = "com.docker.compose.project";

    public static IReadOnlyList<string> GetLabelKeys(IEnumerable<ContainerInfo> containers) =>
        containers
            .SelectMany(container => container.Labels.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static ContainerGroupingResult GroupByLabel(
        IEnumerable<ContainerInfo> containers,
        string labelKey)
    {
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);

        var groups = new Dictionary<string, List<ContainerInfo>>(StringComparer.Ordinal);
        var ungrouped = new List<ContainerInfo>();
        foreach (var container in containers)
        {
            if (!container.Labels.TryGetValue(labelKey, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                ungrouped.Add(container);
                continue;
            }

            if (!groups.TryGetValue(value, out var members))
            {
                members = [];
                groups.Add(value, members);
            }

            members.Add(container);
        }

        return new(
            groups
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new ContainerLabelGroup(group.Key, group.Value))
                .ToArray(),
            ungrouped);
    }
}

public sealed record ContainerLabelGroup(string Value, IReadOnlyList<ContainerInfo> Containers);

public sealed record ContainerGroupingResult(
    IReadOnlyList<ContainerLabelGroup> Groups,
    IReadOnlyList<ContainerInfo> Ungrouped);
