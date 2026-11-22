using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Wessl.App.Services;
using Wessl.Domain;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed record ContainerRowProps(
    ContainerInfo Container,
    ContainerService Service,
    Action<ContainerInfo> OnOpenShell,
    Action<ContainerInfo> OnOpenLogs,
    Action<ContainerInfo> OnOpenDetails,
    bool IsSelected,
    Action ToggleSelection,
    bool ConfirmRemove,
    Action RequestRemove,
    Action CancelRemove);

public sealed class ContainerRow : Component<ContainerRowProps>
{
    public static Element Create(
        ContainerInfo container,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        Action<ContainerInfo> onOpenLogs,
        Action<ContainerInfo> onOpenDetails,
        bool isSelected,
        Action toggleSelection,
        bool confirmRemove,
        Action requestRemove,
        Action cancelRemove) =>
        Component<ContainerRow, ContainerRowProps>(
            new(container, service, onOpenShell, onOpenLogs, onOpenDetails, isSelected, toggleSelection, confirmRemove,
                requestRemove, cancelRemove));

    public override Element Render()
    {
        var (status, setStatus) = UseState<(string Message, bool IsError)?>(null);
        var container = Props.Container;
        var stateGlyph = container.State switch
        {
            ContainerState.Running => "●",
            ContainerState.Stopped => "○",
            _ => "?"
        };
        var stateColor = container.State switch
        {
            ContainerState.Running => Theme.SystemSuccess,
            ContainerState.Stopped => Theme.SystemNeutral,
            _ => Theme.SystemCaution
        };
        var actionPending = Props.Service.IsBusy
            || Props.Service.Status?.ResourceKey.EndsWith("Pending", StringComparison.Ordinal) == true;
        var row = new List<Element>
        {
            Grid([GridSize.Auto, GridSize.Auto, GridSize.Star(2).MinSize(0), GridSize.Star().MinSize(0)], [GridSize.Auto],
                CheckBox(Props.IsSelected, isSelected =>
                    {
                        if (isSelected != Props.IsSelected)
                        {
                            Props.ToggleSelection();
                        }
                    }, label: string.Empty)
                    .AutomationName(container.Name)
                    .ToolTip(AppStrings.Get(Props.IsSelected ? "DeselectContainerButton" : "SelectContainerButton"))
                    .IsEnabled(!actionPending).Grid(column: 0),
                TextBlock(stateGlyph).FontSize(14)
                    .Foreground(stateColor)
                    .ToolTip(AppStrings.GetContainerState(container.State))
                    .AutomationName(AppStrings.GetContainerState(container.State))
                    .Margin(0, 0, 8, 0).Grid(column: 1),
                Caption(container.Name).SemiBold().TextWrapping()
                    .ToolTip(container.Name).Grid(column: 2),
                Caption(container.Image)
                    .Foreground(Theme.SecondaryText)
                    .TextTrimming(TextTrimming.CharacterEllipsis)
                    .ToolTip(container.Image)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .TextAlignment(TextAlignment.Right)
                    .Margin(8, 0, 0, 0).Grid(column: 3)),
        };
        var endpoints = container.PublishedPorts
            .Where(port => port.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            .Select(port =>
            {
                var url = $"http://localhost:{port.HostPort}";
                return Grid([GridSize.Star().MinSize(0), GridSize.Auto, GridSize.Auto], [GridSize.Auto],
                    TextBlock(url).TextWrapping()
                        .VerticalAlignment(VerticalAlignment.Center).Grid(column: 0),
                    IconButton(
                        "\uE8A7",
                        AppStrings.Get("PortOpenButton"),
                        () => _ = OpenUrlAsync(url, setStatus),
                        automationName: AppStrings.Format("PortOpenAccessible", url))
                        .Grid(column: 1),
                    IconButton(
                        "\uE8C8",
                        AppStrings.Get("CopyUrlButton"),
                        () => CopyUrl(url, setStatus),
                        automationName: AppStrings.Format("CopyUrlAccessible", url))
                        .Grid(column: 2));
            }).ToArray();
        if (endpoints.Length > 0)
        {
            row.Add(VStack(4, endpoints));
        }
        var health = container.Diagnostics.Health switch
        {
            ContainerHealthStatus.Starting => AppStrings.Get("ContainerHealthStarting"),
            ContainerHealthStatus.Healthy => AppStrings.Get("ContainerHealthHealthy"),
            ContainerHealthStatus.Unhealthy => AppStrings.Get("ContainerHealthUnhealthy"),
            ContainerHealthStatus.Unavailable => AppStrings.Get("ContainerHealthUnavailable"),
            _ => AppStrings.Get("ContainerHealthUnknown")
        };
        var healthLabel = AppStrings.Format("ContainerHealth", health);
        if (container.Diagnostics.Health is ContainerHealthStatus.Unhealthy or ContainerHealthStatus.Starting)
        {
            row.Add(HStack(4,
                Icon(FontIcon("\uE7BA", fontSize: 14)),
                Caption(healthLabel).SemiBold())
                .AutomationName(healthLabel));
        }
        else
        {
            row.Add(Caption(healthLabel).Foreground(Theme.SecondaryText).TextWrapping());
        }
        if (container.State == ContainerState.Stopped)
        {
            if (container.Diagnostics.ExitCode is { } exitCode)
            {
                row.Add(Caption(AppStrings.Format("ContainerExitCode", exitCode.ToString(CultureInfo.CurrentCulture)))
                    .Foreground(Theme.SecondaryText).TextWrapping());
            }
            if (container.Diagnostics.FinishedAt is { } finishedAt)
            {
                row.Add(Caption(AppStrings.Format(
                        "ContainerExitTime",
                        finishedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)))
                    .Foreground(Theme.SecondaryText).TextWrapping());
            }
        }

        if (!string.IsNullOrWhiteSpace(container.Diagnostics.LatestHealthCheckError))
        {
            row.Add(Caption(AppStrings.Format(
                    "ContainerLatestHealthCheckError",
                    container.Diagnostics.LatestHealthCheckError))
                .Foreground(Theme.SecondaryText).TextWrapping());
        }
        else if (container.Diagnostics.Health == ContainerHealthStatus.Unavailable
            && container.Diagnostics.ExitCode is null
            && container.Diagnostics.FinishedAt is null)
        {
            row.Add(Caption(AppStrings.Get("ContainerDiagnosticUnavailable"))
                .Foreground(Theme.SecondaryText).TextWrapping());
        }

        if (Props.Service.UsageErrors.TryGetValue(container.Id, out var usageError))
        {
            var message = AppStrings.Format(usageError);
            row.Add(InfoBar(message: message).Error()
                with { IsOpen = true, IsClosable = false });
        }
        else if (Props.Service.Usage.TryGetValue(container.Id, out var usage))
        {
            var memory = usage.MemoryPercent.Length > 0
                ? $"{usage.MemoryUsage} ({usage.MemoryPercent})"
                : usage.MemoryUsage;
            var details = usage.NetworkIo.Length > 0 && usage.BlockIo.Length > 0 && usage.Processes.Length > 0
                ? AppStrings.Format("ContainerUsageDetails", usage.NetworkIo, usage.BlockIo, usage.Processes)
                : string.Empty;
            var usageLines = new List<Element>
            {
                UsageLine(AppStrings.Format("ContainerUsage", usage.CpuPercent, memory))
            };
            if (details.Length > 0)
            {
                usageLines.Add(UsageLine(details));
            }
            row.Add(VStack(4, usageLines.ToArray()));
        }
        else if (container.State == ContainerState.Running)
        {
            row.Add(UsageLine(AppStrings.Get("ContainerUsageLoading")));
        }

        if (status is { } feedback)
        {
            var message = InfoBar(message: feedback.Message);
            row.Add((feedback.IsError ? message.Error() : message.Success())
                with { IsOpen = true, IsClosable = false });
        }

        var actions = new List<Element>
        {
            IconButton("\uE946", AppStrings.Get("ContainerDetailsButton"), () => Props.OnOpenDetails(container)),
            container.State == ContainerState.Running
                ? IconButton("\uE71A", AppStrings.Get("StopButton"), () => _ = Props.Service.StopAsync(container), subtle: false)
                    .IsEnabled(!actionPending)
                : IconButton("\uE768", AppStrings.Get("StartButton"), () => _ = Props.Service.StartAsync(container), accent: true)
                    .IsEnabled(!actionPending),
        };
        if (container.State == ContainerState.Running)
        {
            actions.Add(IconButton("\uE72C", AppStrings.Get("RestartButton"), () => _ = Props.Service.RestartAsync(container))
                .IsEnabled(!actionPending));
            actions.Add(IconButton("\uE756", AppStrings.Get("OpenShellButton"), () => Props.OnOpenShell(container))
                .IsEnabled(!actionPending));
            actions.Add(IconButton("\uE8A5", AppStrings.Get("OpenLogsButton"), () => Props.OnOpenLogs(container))
                .IsEnabled(!actionPending));
        }

        if (Props.ConfirmRemove)
        {
            row.Add(TextBlock(AppStrings.Format("RemoveConfirmation", container.Name)).TextWrapping());
            row.Add(FlexRow(
                Button(AppStrings.Get("ConfirmRemoveButton"), () => _ = Props.Service.RemoveAsync(container)).IsEnabled(!actionPending),
                Button(AppStrings.Get("CancelButton"), Props.CancelRemove)) with
                { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 });
        }
        else
        {
            actions.Add(IconButton("\uE74D", AppStrings.Get("RemoveButton"), Props.RequestRemove)
                .IsEnabled(!actionPending));
        }

        row.Add(FlexRow(actions.ToArray()) with { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 });
        return Card(VStack(8, row.ToArray())).Padding(16);
    }

    private static Element UsageLine(string text)
    {
        return Caption(text).Foreground(Theme.SecondaryText).TextWrapping();
    }

    private static Element IconButton(
        string glyph,
        string label,
        Action action,
        bool accent = false,
        bool subtle = true,
        string? automationName = null)
    {
        // Reactor applies native button styles only on mount; keep each action's identity distinct.
        var button = Button(
            Icon(FontIcon(glyph, fontSize: 16)),
            action)
            .WithKey(glyph)
            .Width(40)
            .Height(40)
            .ToolTip(label)
            .AutomationName(automationName ?? label);

        if (accent)
        {
            return button.AccentButton().Padding(0);
        }

        return (subtle ? button.SubtleButton() : button).Padding(0);
    }

    private static async Task OpenUrlAsync(string url, Action<(string Message, bool IsError)?> setStatus)
    {
        try
        {
            var launched = await Launcher.LaunchUriAsync(new Uri(url));
            setStatus((AppStrings.Format(launched ? "PortOpenSuccess" : "PortOpenFailure", url), !launched));
        }
        catch (Exception exception)
        {
            setStatus((AppStrings.Format("PortOpenError", url, exception.Message), true));
        }
    }

    private static void CopyUrl(string url, Action<(string Message, bool IsError)?> setStatus)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(url);
            Clipboard.SetContent(package);
            setStatus((AppStrings.Format("PortCopySuccess", url), false));
        }
        catch (Exception exception)
        {
            setStatus((AppStrings.Format("PortCopyError", url, exception.Message), true));
        }
    }
}
