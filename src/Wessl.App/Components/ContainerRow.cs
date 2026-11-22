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
        bool confirmRemove,
        Action requestRemove,
        Action cancelRemove) =>
        Component<ContainerRow, ContainerRowProps>(
            new(container, service, onOpenShell, onOpenLogs, confirmRemove, requestRemove, cancelRemove));

    public override Element Render()
    {
        var (status, setStatus) = UseState(string.Empty);
        var container = Props.Container;
        var ports = container.Ports.Count == 0
            ? AppStrings.Get("NoPorts")
            : string.Join(", ", container.Ports);
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
        var actionPending = Props.Service.Status?.ResourceKey.EndsWith("Pending", StringComparison.Ordinal) == true;
        var row = new List<Element>
        {
            Grid([GridSize.Auto, GridSize.Star(), GridSize.Star()], [GridSize.Auto],
                TextBlock(stateGlyph).FontSize(14)
                    .Foreground(stateColor)
                    .ToolTip(AppStrings.GetContainerState(container.State))
                    .AutomationName(AppStrings.GetContainerState(container.State))
                    .Margin(0, 0, 8, 0).Grid(column: 0),
                TextBlock(container.Name).FontSize(16).SemiBold().TextWrapping().Grid(column: 1),
                Caption(container.Image)
                    .Foreground(Theme.SecondaryText)
                    .TextTrimming(TextTrimming.CharacterEllipsis)
                    .ToolTip(container.Image)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .TextAlignment(TextAlignment.Right)
                    .Margin(8, 0, 0, 0).Grid(column: 2)),
            Caption(AppStrings.Format("PortsLabel", ports)).Foreground(Theme.SecondaryText).TextWrapping(),
        };
        row.AddRange(container.PublishedPorts
            .Where(port => port.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            .Select(port =>
            {
                var url = $"http://localhost:{port.HostPort}";
                return FlexRow(
                    TextBlock(url).TextWrapping(),
                    IconButton(
                        "\uE8A7",
                        AppStrings.Get("PortOpenButton"),
                        () => _ = OpenUrlAsync(url, setStatus)),
                    IconButton(
                        "\uE8C8",
                        AppStrings.Get("CopyUrlButton"),
                        () => CopyUrl(url, setStatus))) with
                    { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 };
            }));
        var health = container.Diagnostics.Health switch
        {
            ContainerHealthStatus.Starting => AppStrings.Get("ContainerHealthStarting"),
            ContainerHealthStatus.Healthy => AppStrings.Get("ContainerHealthHealthy"),
            ContainerHealthStatus.Unhealthy => AppStrings.Get("ContainerHealthUnhealthy"),
            ContainerHealthStatus.Unavailable => AppStrings.Get("ContainerHealthUnavailable"),
            _ => AppStrings.Get("ContainerHealthUnknown")
        };
        row.Add(Caption(AppStrings.Format("ContainerHealth", health))
            .Foreground(Theme.SecondaryText).TextWrapping());
        if (container.State == ContainerState.Stopped)
        {
            var exitCode = container.Diagnostics.ExitCode?.ToString(CultureInfo.CurrentCulture)
                ?? AppStrings.Get("ContainerDiagnosticUnavailable");
            var exitTime = container.Diagnostics.FinishedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                ?? AppStrings.Get("ContainerDiagnosticUnavailable");
            row.Add(Caption(AppStrings.Format("ContainerExitCode", exitCode))
                .Foreground(Theme.SecondaryText).TextWrapping());
            row.Add(Caption(AppStrings.Format("ContainerExitTime", exitTime))
                .Foreground(Theme.SecondaryText).TextWrapping());
        }

        row.Add(Caption(AppStrings.Format(
                "ContainerLatestHealthCheckError",
                container.Diagnostics.LatestHealthCheckError ?? AppStrings.Get("ContainerDiagnosticUnavailable")))
            .Foreground(Theme.SecondaryText).TextWrapping());

        if (Props.Service.UsageErrors.TryGetValue(container.Id, out var usageError))
        {
            row.Add(InfoBar(message: AppStrings.Format(usageError)).Error()
                with { IsOpen = true, IsClosable = false });
        }
        else if (Props.Service.Usage.TryGetValue(container.Id, out var usage))
        {
            var memory = usage.MemoryPercent.Length > 0
                ? $"{usage.MemoryUsage} ({usage.MemoryPercent})"
                : usage.MemoryUsage;
            row.Add(Caption(AppStrings.Format("ContainerUsage", usage.CpuPercent, memory))
                .Foreground(Theme.SecondaryText).TextWrapping());
            if (usage.NetworkIo.Length > 0 && usage.BlockIo.Length > 0 && usage.Processes.Length > 0)
            {
                row.Add(Caption(AppStrings.Format("ContainerUsageDetails", usage.NetworkIo, usage.BlockIo, usage.Processes))
                    .Foreground(Theme.SecondaryText).TextWrapping());
            }
        }
        else if (container.State == ContainerState.Running)
        {
            row.Add(Caption(AppStrings.Get("ContainerUsageLoading")).Foreground(Theme.SecondaryText));
        }

        if (status.Length > 0)
        {
            row.Add(TextBlock(status).TextWrapping());
        }

        var actions = new List<Element>
        {
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

    private static Element IconButton(
        string glyph,
        string label,
        Action action,
        bool accent = false,
        bool subtle = true)
    {
        // Reactor applies native button styles only on mount; keep each action's identity distinct.
        var button = Button(
            Icon(FontIcon(glyph, fontSize: 16)),
            action)
            .WithKey(glyph)
            .Width(40)
            .Height(40)
            .ToolTip(label)
            .AutomationName(label);

        if (accent)
        {
            return button.AccentButton().Padding(0);
        }

        return (subtle ? button.SubtleButton() : button).Padding(0);
    }

    private static async Task OpenUrlAsync(string url, Action<string> setStatus)
    {
        try
        {
            var launched = await Launcher.LaunchUriAsync(new Uri(url));
            setStatus(AppStrings.Format(launched ? "PortOpenSuccess" : "PortOpenFailure", url));
        }
        catch (Exception exception)
        {
            setStatus(AppStrings.Format("PortOpenError", url, exception.Message));
        }
    }

    private static void CopyUrl(string url, Action<string> setStatus)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(url);
            Clipboard.SetContent(package);
            setStatus(AppStrings.Format("PortCopySuccess", url));
        }
        catch (Exception exception)
        {
            setStatus(AppStrings.Format("PortCopyError", url, exception.Message));
        }
    }
}
