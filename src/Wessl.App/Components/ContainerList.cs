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
        var (selectedIds, setSelectedIds) = UseState(Array.Empty<string>());
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
        var selectedContainers = Props.Containers
            .Where(container => selectedIds.Contains(container.Id, StringComparer.Ordinal))
            .ToArray();
        var startableContainers = selectedContainers
            .Where(container => container.State == ContainerState.Stopped)
            .ToArray();
        var restartableContainers = selectedContainers
            .Where(container => container.State == ContainerState.Running)
            .ToArray();
        var removeableContainers = startableContainers;
        var selectionActions = FlexRow(
            TextBlock(AppStrings.Format("BulkSelectionCount", selectedContainers.Length))
                .VerticalAlignment(Microsoft.UI.Xaml.VerticalAlignment.Center),
            Button(AppStrings.Format("BulkStartSelectedButton", startableContainers.Length),
                () => _ = service.StartSelectedAsync(startableContainers))
                .AccentButton()
                .IsEnabled(!service.IsBusy && startableContainers.Length > 0),
            Button(AppStrings.Format("BulkRestartSelectedButton", restartableContainers.Length),
                () => _ = service.RestartSelectedAsync(restartableContainers))
                .IsEnabled(!service.IsBusy && restartableContainers.Length > 0),
            Button(AppStrings.Format("BulkRemoveSelectedButton", removeableContainers.Length),
                () => Props.RequestBulkRemove(removeableContainers))
                .SubtleButton()
                .IsEnabled(!service.IsBusy && removeableContainers.Length > 0),
            Button(AppStrings.Get("BulkClearSelectionButton"), () => setSelectedIds(Array.Empty<string>()))
                .SubtleButton()
                .IsEnabled(!service.IsBusy && selectedContainers.Length > 0)) with
        {
            Wrap = FlexWrap.Wrap,
            ColumnGap = 8,
            RowGap = 8
        };

        var controls = VStack(8,
            selectionActions,
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
            });

        Element list = filteredContainers.Count == 0
            ? TextBlock(AppStrings.Get("ContainersNoMatches"))
                .TextWrapping(TextWrapping.Wrap)
            : ScrollViewer(VStack(
                8,
                filteredContainers.Select(container =>
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
                        Props.CancelRemove).WithKey(container.Id)).ToArray()))
                .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);

        return Grid(
            [GridSize.Star()],
            [GridSize.Auto, GridSize.Star()],
            controls.Grid(row: 0),
            list.Grid(row: 1));
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
