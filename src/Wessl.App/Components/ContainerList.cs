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
    Action<ContainerInfo> OnOpenDetails,
    string? PendingRemoval,
    Action<string> RequestRemove,
    Action CancelRemove,
    Action<IReadOnlyList<ContainerInfo>> RequestBulkRemove,
    string LanguageTag);

public sealed class ContainerList : Component<ContainerListProps>
{
    public static Element Create(
        IReadOnlyList<ContainerInfo> containers,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        Action<ContainerInfo> onOpenLogs,
        Action<ContainerInfo> onOpenDetails,
        string? pendingRemoval,
        Action<string> requestRemove,
        Action cancelRemove,
        Action<IReadOnlyList<ContainerInfo>> requestBulkRemove,
        string languageTag) =>
        Component<ContainerList, ContainerListProps>(
            new(containers, service, onOpenShell, onOpenLogs, onOpenDetails, pendingRemoval, requestRemove, cancelRemove,
                requestBulkRemove, languageTag));

    public override Element Render()
    {
        var (searchText, setSearchText) = UseState(string.Empty);
        var (stateFilter, setStateFilter) = UseState<ContainerState?>(null);
        var (imageFilter, setImageFilter) = UseState<string?>(null);
        var (sortOrder, setSortOrder) = UseState(ContainerSortOrder.Name);
        var (groupingSelection, setGroupingSelection) = UseState(string.Empty);
        var (selectedIds, setSelectedIds) = UseState(Array.Empty<string>());
        var (filtersExpanded, setFiltersExpanded) = UseState(false);
        var service = Props.Service;

        if (service.IsWslcUnavailable && !service.HasRefreshed)
        {
            return Component<UnavailableWslc>();
        }

        if (!service.HasRefreshed && service.IsRefreshing)
        {
            return ProgressRing();
        }

        if (!service.HasRefreshed && service.Error is not null)
        {
            return VStack(8,
                TextBlock(AppStrings.Get("ContainersLoadFailed"))
                    .TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync()));
        }

        if (!service.HasRefreshed)
        {
            return VStack(8,
                TextBlock(AppStrings.Get("ContainersLoading"))
                    .TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync()));
        }

        if (Props.Containers.Count == 0)
        {
            var emptyContent = new List<Element>();
            if (service.IsRefreshing || service.RefreshError is not null)
            {
                emptyContent.Add(InfoBar(message: AppStrings.Get(service.IsRefreshing && service.RefreshError is null
                        ? "ContainersRefreshingPreviousData"
                        : "ContainersShowingPreviousData"))
                    .Informational() with { IsOpen = true, IsClosable = false });
            }
            emptyContent.Add(TextBlock(AppStrings.Get("EmptyContainers"))
                .TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap));
            emptyContent.Add(Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync())
                .IsEnabled(!service.IsRefreshing));
            return VStack(8, emptyContent.ToArray());
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

        var selectedContainers = Props.Containers
            .Where(container => selectedIds.Contains(container.Id, StringComparer.Ordinal))
            .ToArray();
        var hiddenSelectedCount = ContainerListQuery.CountHiddenSelected(selectedContainers, filteredContainers);
        var startableContainers = selectedContainers
            .Where(container => container.State == ContainerState.Stopped)
            .ToArray();
        var restartableContainers = selectedContainers
            .Where(container => container.State == ContainerState.Running)
            .ToArray();
        var removeableContainers = startableContainers;
        var controlSections = new List<Element>
        {
            TextBox(searchText, setSearchText)
                .PlaceholderText(AppStrings.Get("ContainersSearchPlaceholder"))
                .AutomationName(AppStrings.Get("ContainersSearchLabel")),
            Button(AppStrings.Get(filtersExpanded ? "ContainersHideFilters" : "ContainersShowFilters"),
                () => setFiltersExpanded(!filtersExpanded))
        };
        if (selectedContainers.Length > 0)
        {
            var selectionControls = new List<Element>
            {
                TextBlock(AppStrings.Format("BulkSelectionCount", selectedContainers.Length))
                    .VerticalAlignment(Microsoft.UI.Xaml.VerticalAlignment.Center)
            };
            if (hiddenSelectedCount > 0)
            {
                selectionControls.Add(TextBlock(AppStrings.Format(
                        "BulkHiddenSelectionCount",
                        hiddenSelectedCount))
                    .TextWrapping(TextWrapping.Wrap));
                selectionControls.Add(Button(AppStrings.Get("ContainersReviewHiddenSelection"), () =>
                {
                    setSearchText(string.Empty);
                    setStateFilter(null);
                    setImageFilter(null);
                    setFiltersExpanded(true);
                }));
            }

            selectionControls.Add(Button(AppStrings.Format("BulkStartSelectedButton", startableContainers.Length),
                    () => _ = service.StartSelectedAsync(startableContainers))
                .AccentButton()
                .IsEnabled(!service.IsBusy && startableContainers.Length > 0));
            selectionControls.Add(Button(AppStrings.Format("BulkRestartSelectedButton", restartableContainers.Length),
                    () => _ = service.RestartSelectedAsync(restartableContainers))
                .IsEnabled(!service.IsBusy && restartableContainers.Length > 0));
            selectionControls.Add(Button(AppStrings.Format("BulkRemoveSelectedButton", removeableContainers.Length),
                    () => Props.RequestBulkRemove(removeableContainers))
                .SubtleButton()
                .IsEnabled(!service.IsBusy && removeableContainers.Length > 0));
            selectionControls.Add(Button(AppStrings.Get("BulkClearSelectionButton"),
                    () => setSelectedIds(Array.Empty<string>()))
                .SubtleButton()
                .IsEnabled(!service.IsBusy));
            controlSections.Add(FlexRow(selectionControls.ToArray()) with
            {
                Wrap = FlexWrap.Wrap,
                ColumnGap = 8,
                RowGap = 8
            });
        }

        if (filtersExpanded)
        {
            controlSections.Add(FlexRow(
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
            });
            controlSections.Add(VStack(4,
                TextBlock(AppStrings.Get("ContainersGroupByLabelLabel")),
                ComboBox(groupingLabels, groupingIndex, index =>
                {
                    if (index >= 0)
                    {
                        setGroupingSelection(groupingKeys[index] ?? "\0");
                    }
                })
                .AutomationName(AppStrings.Get("ContainersGroupByLabelLabel"))
                .ToolTip(AppStrings.Get(labelKeys.Count == 0
                    ? "ContainersGroupNoMetadata"
                    : !hasComposeProjectLabel && groupingSelection.Length == 0
                        ? "ContainersGroupComposeUnavailable"
                        : "ContainersGroupByLabelLabel"))
                .WithKey($"containers-group-{Props.LanguageTag}-{string.Join("|", labelKeys)}")));
        }
        var controls = VStack(8, controlSections.ToArray());

        Element CreateRow(ContainerInfo container) =>
            ContainerRow.Create(
                container,
                service,
                Props.OnOpenShell,
                Props.OnOpenLogs,
                Props.OnOpenDetails,
                selectedIds.Contains(container.Id, StringComparer.Ordinal),
                () =>
                {
                    var nextSelectedIds = selectedIds.ToHashSet(StringComparer.Ordinal);
                    if (!nextSelectedIds.Add(container.Id))
                    {
                        nextSelectedIds.Remove(container.Id);
                    }
                    setSelectedIds(nextSelectedIds.ToArray());
                },
                Props.PendingRemoval == container.Id,
                () => Props.RequestRemove(container.Id),
                Props.CancelRemove).WithKey(container.Id);
        Element list;
        if (filteredContainers.Count == 0)
        {
            list = TextBlock(AppStrings.Get("ContainersNoMatches"))
                .TextWrapping(TextWrapping.Wrap);
        }
        else if (groupByLabel is null)
        {
            list = VStack(8, filteredContainers.Select(CreateRow).ToArray());
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

            list = VStack(8, sections.ToArray());
        }

        var content = new List<Element> { controls };
        if (service.HasRefreshed && (service.IsRefreshing || service.RefreshError is not null))
        {
            content.Add(InfoBar(message: AppStrings.Get(service.IsRefreshing && service.RefreshError is null
                    ? "ContainersRefreshingPreviousData"
                    : "ContainersShowingPreviousData"))
                .Informational() with { IsOpen = true, IsClosable = false });
        }
        if (service.DiagnosticsError is { } diagnosticsError)
        {
            content.Add(InfoBar(message: AppStrings.Format(diagnosticsError)).Error()
                with { IsOpen = true, IsClosable = false });
        }
        content.Add(list);

        // Disabling horizontal input alone does not constrain the content's measured width.
        return ScrollViewer(VStack(8, content.ToArray()))
            .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled) with
            {
                HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto
            };
    }

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
