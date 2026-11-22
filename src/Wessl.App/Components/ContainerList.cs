using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Wessl.App.Services;
using Wessl.Domain;
using Windows.ApplicationModel.DataTransfer;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed record ContainerListProps(
    IReadOnlyList<ContainerInfo> Containers,
    ContainerService Service,
    Action<ContainerInfo> OnOpenShell,
    Action<ContainerInfo> OnOpenLogs,
    string? PendingRemoval,
    Action<string> RequestRemove,
    Action CancelRemove,
    string LanguageTag);

public sealed class ContainerList : Component<ContainerListProps>
{
    public static Element Create(
        IReadOnlyList<ContainerInfo> containers,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        Action<ContainerInfo> onOpenLogs,
        string? pendingRemoval,
        Action<string> requestRemove,
        Action cancelRemove,
        string languageTag) =>
        Component<ContainerList, ContainerListProps>(
            new(containers, service, onOpenShell, onOpenLogs, pendingRemoval, requestRemove, cancelRemove, languageTag));

    public override Element Render()
    {
        var (searchText, setSearchText) = UseState(string.Empty);
        var (stateFilter, setStateFilter) = UseState<ContainerState?>(null);
        var (imageFilter, setImageFilter) = UseState<string?>(null);
        var (sortOrder, setSortOrder) = UseState(ContainerSortOrder.Name);
        var (groupingSelection, setGroupingSelection) = UseState(string.Empty);
        var service = Props.Service;

        if (service.IsWslcUnavailable)
        {
            return Component<UnavailableWslc>();
        }

        if (!service.HasRefreshed && service.Error is null)
        {
            return VStack(8,
                TextBlock(AppStrings.Get("ContainersLoading")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                ProgressRing());
        }

        if (Props.Containers.Count == 0)
        {
            return VStack(8,
                TextBlock(AppStrings.Get("EmptyContainers")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync()));
        }

        var states = new ContainerState?[]
        {
            null,
            ContainerState.Running,
            ContainerState.Stopped,
            ContainerState.Unknown
        };
        var stateIndex = Array.IndexOf(states, stateFilter);
        var stateLabels = new[]
        {
            AppStrings.Get("ContainersFilterAllStates"),
            AppStrings.Get("ContainerStateRunning"),
            AppStrings.Get("ContainerStateStopped"),
            AppStrings.Get("ContainerStateUnknown")
        };
        var imageOptions = Props.Containers
            .Select(container => container.Image)
            .Append(imageFilter)
            .Where(image => !string.IsNullOrWhiteSpace(image))
            .Select(image => image!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(image => image, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var imageLabels = new[] { AppStrings.Get("ContainersFilterAllImages") }.Concat(imageOptions).ToArray();
        var imageIndex = imageFilter is null
            ? 0
            : Array.FindIndex(imageOptions, image => string.Equals(image, imageFilter, StringComparison.OrdinalIgnoreCase)) + 1;
        var sortOrders = new[]
        {
            ContainerSortOrder.Name,
            ContainerSortOrder.State,
            ContainerSortOrder.Image
        };
        var sortLabels = new[]
        {
            AppStrings.Get("ContainersSortName"),
            AppStrings.Get("ContainersSortState"),
            AppStrings.Get("ContainersSortImage")
        };
        var sortIndex = Array.IndexOf(sortOrders, sortOrder);
        var filteredContainers = ContainerListQuery.Apply(
            Props.Containers,
            searchText,
            stateFilter,
            imageFilter,
            sortOrder);
        var labelKeys = ContainerGrouping.GetLabelKeys(Props.Containers);
        var hasComposeProjectLabel = labelKeys.Contains(
            ContainerGrouping.ComposeProjectLabel,
            StringComparer.Ordinal);
        var groupByLabel = groupingSelection switch
        {
            "" => hasComposeProjectLabel ? ContainerGrouping.ComposeProjectLabel : null,
            "\0" => null,
            _ => labelKeys.Contains(groupingSelection, StringComparer.Ordinal) ? groupingSelection : null
        };
        var groupingKeys = new string?[] { null }.Concat(labelKeys).ToArray();
        var groupingLabels = new[] { AppStrings.Get("ContainersGroupNone") }
            .Concat(labelKeys.Select(key => key == ContainerGrouping.ComposeProjectLabel
                ? AppStrings.Get("ContainersGroupCompose")
                : AppStrings.Format("ContainersGroupLabel", key)))
            .ToArray();
        var groupingIndex = Array.FindIndex(
            groupingKeys,
            key => string.Equals(key, groupByLabel, StringComparison.Ordinal));

        var controlSections = new List<Element>
        {
            TextBox(searchText, setSearchText)
                .PlaceholderText(AppStrings.Get("ContainersSearchPlaceholder"))
                .AutomationName(AppStrings.Get("ContainersSearchLabel")),
            FlexRow(
                VStack(4,
                    TextBlock(AppStrings.Get("ContainersStateFilterLabel")),
                    ComboBox(
                        stateLabels,
                        stateIndex,
                        index => setStateFilter(index >= 0 ? states[index] : null))
                        .AutomationName(AppStrings.Get("ContainersStateFilterLabel"))
                        .WithKey($"containers-state-{Props.LanguageTag}")),
                VStack(4,
                    TextBlock(AppStrings.Get("ContainersImageFilterLabel")),
                    ComboBox(imageLabels, imageIndex,
                        index => setImageFilter(index > 0 ? imageOptions[index - 1] : null))
                        .AutomationName(AppStrings.Get("ContainersImageFilterLabel"))
                        .WithKey($"containers-image-{Props.LanguageTag}-{string.Join("|", imageOptions)}")),
                VStack(4,
                    TextBlock(AppStrings.Get("ContainersSortLabel")),
                    ComboBox(sortLabels, sortIndex,
                        index => { if (index >= 0) setSortOrder(sortOrders[index]); })
                        .AutomationName(AppStrings.Get("ContainersSortLabel"))
                        .WithKey($"containers-sort-{Props.LanguageTag}")),
                Button(AppStrings.Get("ContainersResetFilters"), () =>
                {
                    setSearchText(string.Empty);
                    setStateFilter(null);
                    setImageFilter(null);
                })) with
            {
                Wrap = FlexWrap.Wrap,
                ColumnGap = 8,
                RowGap = 8
            },
            VStack(4,
                TextBlock(AppStrings.Get("ContainersGroupByLabelLabel")),
                ComboBox(groupingLabels, groupingIndex, index =>
                {
                    if (index >= 0)
                    {
                        setGroupingSelection(groupingKeys[index] ?? "\0");
                    }
                })
                .AutomationName(AppStrings.Get("ContainersGroupByLabelLabel"))
                .WithKey($"containers-group-{Props.LanguageTag}-{string.Join("|", labelKeys)}"))
        };
        if (labelKeys.Count == 0)
        {
            controlSections.Add(TextBlock(AppStrings.Get("ContainersGroupNoMetadata"))
                .TextWrapping(TextWrapping.Wrap));
        }
        else if (!hasComposeProjectLabel && groupingSelection.Length == 0)
        {
            controlSections.Add(TextBlock(AppStrings.Get("ContainersGroupComposeUnavailable"))
                .TextWrapping(TextWrapping.Wrap));
        }
        var controls = VStack(8, controlSections.ToArray());

        Element list;
        if (filteredContainers.Count == 0)
        {
            list = TextBlock(AppStrings.Get("ContainersNoMatches"))
                .TextWrapping(TextWrapping.Wrap);
        }
        else if (groupByLabel is null)
        {
            list = ScrollViewer(VStack(8, filteredContainers.Select(CreateRow).ToArray()))
                .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);
        }
        else
        {
            var grouping = ContainerGrouping.GroupByLabel(filteredContainers, groupByLabel);
            var labelName = groupByLabel == ContainerGrouping.ComposeProjectLabel
                ? AppStrings.Get("ContainersGroupCompose")
                : AppStrings.Format("ContainersGroupLabel", groupByLabel);
            var sections = grouping.Groups
                .Select(group => (Element)ContainerGroupSection.Create(
                    group,
                    labelName,
                    service,
                    CreateRow))
                .ToList();
            if (grouping.Ungrouped.Count > 0)
            {
                sections.Add(TextBlock(AppStrings.Format("ContainersWithoutGroupLabel", groupByLabel))
                    .TextWrapping(TextWrapping.Wrap));
                sections.AddRange(grouping.Ungrouped.Select(CreateRow));
            }

            list = ScrollViewer(VStack(8, sections.ToArray()))
                .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);
        }

        return Grid(
            [GridSize.Star()],
            [GridSize.Auto, GridSize.Star()],
            controls.Grid(row: 0),
            list.Grid(row: 1));
    }

    private Element CreateRow(ContainerInfo container) =>
        ContainerRow.Create(
            container,
            Props.Service,
            Props.OnOpenShell,
            Props.OnOpenLogs,
            Props.PendingRemoval == container.Id,
            () => Props.RequestRemove(container.Id),
            Props.CancelRemove).WithKey(container.Id);
}

public sealed record ContainerGroupSectionProps(
    ContainerLabelGroup Group,
    string LabelName,
    ContainerService Service,
    Func<ContainerInfo, Element> CreateRow);

public sealed class ContainerGroupSection : Component<ContainerGroupSectionProps>
{
    public static Element Create(
        ContainerLabelGroup group,
        string labelName,
        ContainerService service,
        Func<ContainerInfo, Element> createRow) =>
        Component<ContainerGroupSection, ContainerGroupSectionProps>(
            new(group, labelName, service, createRow));

    public override Element Render()
    {
        var group = Props.Group;
        var stopped = group.Containers.Count(container => container.State == ContainerState.Stopped);
        var running = group.Containers.Count(container => container.State == ContainerState.Running);
        var actionPending = Props.Service.Status?.ResourceKey.EndsWith("Pending", StringComparison.Ordinal) == true;

        return Card(VStack(8,
            TextBlock(AppStrings.Format(
                    "ContainerGroupTitle",
                    $"{Props.LabelName} · {group.Value}",
                    group.Containers.Count))
                .FontSize(15)
                .SemiBold()
                .TextWrapping(TextWrapping.Wrap),
            FlexRow(
                Button(AppStrings.Get("GroupStartButton"), () =>
                    _ = Props.Service.StartGroupAsync(group.Value, group.Containers))
                    .AccentButton()
                    .IsEnabled(stopped > 0 && !actionPending),
                Button(AppStrings.Get("GroupStopButton"), () =>
                    _ = Props.Service.StopGroupAsync(group.Value, group.Containers))
                    .IsEnabled(running > 0 && !actionPending)) with
            {
                Wrap = FlexWrap.Wrap,
                ColumnGap = 8,
                RowGap = 8
            },
            VStack(8, group.Containers.Select(Props.CreateRow).ToArray())))
            .Padding(12);
    }
}

public sealed class UnavailableWslc : Component
{
    public override Element Render()
    {
        var (status, setStatus) = UseState(string.Empty);
        return VStack(8,
            TextBlock(AppStrings.Get("WslcUnavailable")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
            Button(AppStrings.Get("CopyUpdateCommand"), () =>
            {
                try
                {
                    var package = new DataPackage();
                    package.SetText("wsl --update");
                    Clipboard.SetContent(package);
                    setStatus(AppStrings.Get("UpdateCommandCopied"));
                }
                catch (Exception exception)
                {
                    setStatus(AppStrings.Format("UpdateCommandCopyError", exception.Message));
                }
            }),
            TextBlock(status).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap));
    }
}
