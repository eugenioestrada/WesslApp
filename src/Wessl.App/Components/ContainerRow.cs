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
    bool ConfirmRemove,
    Action RequestRemove,
    Action CancelRemove);

public sealed class ContainerRow : Component<ContainerRowProps>
{
    public static Element Create(
        ContainerInfo container,
        ContainerService service,
        Action<ContainerInfo> onOpenShell,
        bool confirmRemove,
        Action requestRemove,
        Action cancelRemove) =>
        Component<ContainerRow, ContainerRowProps>(
            new(container, service, onOpenShell, confirmRemove, requestRemove, cancelRemove));

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
                    Button(url, () => _ = OpenUrlAsync(url, setStatus)),
                    Button(AppStrings.Get("CopyUrlButton"), () => CopyUrl(url, setStatus))) with
                    { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 };
            }));
        if (status.Length > 0)
        {
            row.Add(TextBlock(status).TextWrapping());
        }

        var actions = new List<Element>
        {
            container.State == ContainerState.Running
                ? Button(AppStrings.Get("StopButton"), () => _ = Props.Service.StopAsync(container)).IsEnabled(!actionPending)
                : Button(AppStrings.Get("StartButton"), () => _ = Props.Service.StartAsync(container)).AccentButton().IsEnabled(!actionPending),
        };
        if (container.State == ContainerState.Running)
        {
            actions.Add(Button(AppStrings.Get("RestartButton"), () => _ = Props.Service.RestartAsync(container)).SubtleButton().IsEnabled(!actionPending));
            actions.Add(Button(AppStrings.Get("OpenShellButton"), () => Props.OnOpenShell(container)).SubtleButton().IsEnabled(!actionPending));
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
            actions.Add(Button(AppStrings.Get("RemoveButton"), Props.RequestRemove).SubtleButton().IsEnabled(!actionPending));
        }

        row.Add(FlexRow(actions.ToArray()) with { Wrap = FlexWrap.Wrap, ColumnGap = 8, RowGap = 8 });
        return Card(VStack(8, row.ToArray())).Padding(16);
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
