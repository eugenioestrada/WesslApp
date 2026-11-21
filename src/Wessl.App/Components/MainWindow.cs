using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class MainWindow(ContainerService containers, ContainerPolling polling) : Component
{
    public override Element Render()
    {
        var (_, setRevision) = UseState(0);
        var (pendingRemoval, setPendingRemoval) = UseState<string?>(null);
        UseEffect(() =>
        {
            var revision = 0;
            void OnChanged() => setRevision(++revision);
            containers.Changed += OnChanged;
            return () => containers.Changed -= OnChanged;
        }, containers);

        var runningCount = containers.Containers.Count(container => container.State == ContainerState.Running);
        var refreshLabel = AppStrings.Get("RefreshButton");
        var messages = new List<Element>();
        if (containers.Error is { } error)
        {
            messages.Add(InfoBar(message: AppStrings.Format(error)).Error() with { IsOpen = true, IsClosable = false });
        }
        if (containers.Status is { } status)
        {
            var message = InfoBar(message: AppStrings.Format(status));
            messages.Add((status.ResourceKey.EndsWith("Pending", StringComparison.Ordinal) ? message.Informational() : message.Success())
                with { IsOpen = true, IsClosable = false });
        }
        if (containers.ShellError is { } shellError)
        {
            messages.Add(InfoBar(message: AppStrings.Format("OpenShellError", shellError)).Error()
                with { IsOpen = true, IsClosable = false });
        }

        return Grid(
            [GridSize.Star()],
            [GridSize.Auto, GridSize.Auto, GridSize.Auto, GridSize.Star()],
            Grid([GridSize.Star(), GridSize.Auto], [GridSize.Auto],
                    TextBlock("Wessl").FontSize(16).SemiBold()
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 0),
                    Button(Icon(FontIcon("\uE72C", "Segoe Fluent Icons, Segoe MDL2 Assets", 14)), () => _ = polling.RefreshAsync())
                        .SubtleButton()
                        .ToolTip(refreshLabel)
                        .AutomationName(refreshLabel)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 1))
                .Margin(16, 4, 16, 0)
                .Grid(row: 0),
            TextBlock(AppStrings.Format("ContainerSummary", containers.Containers.Count, runningCount))
                .TextWrapping()
                .Margin(16, 8, 16, 8)
                .Grid(row: 1),
            VStack(4, messages.ToArray()).Margin(16, 0).Grid(row: 2),
            ContainerList.Create(
                containers.Containers,
                containers,
                containers.OpenShell,
                pendingRemoval,
                id => setPendingRemoval(id),
                () => setPendingRemoval(null)).Margin(16, 0, 16, 16).Grid(row: 3)
        );
    }
}
