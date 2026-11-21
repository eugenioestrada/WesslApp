using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
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
        var row = new List<Element>
        {
            TextBlock(container.Name).SemiBold(),
            TextBlock(AppStrings.Format("ContainerDetails", container.Image, AppStrings.GetContainerState(container.State))),
            TextBlock(AppStrings.Format("PortsLabel", ports)),
        };
        row.AddRange(container.PublishedPorts
            .Where(port => port.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            .Select(port =>
            {
                var url = $"http://localhost:{port.HostPort}";
                return HStack(8,
                    Button(url, () => _ = OpenUrlAsync(url, setStatus)),
                    Button(AppStrings.Get("CopyUrlButton"), () => CopyUrl(url, setStatus)));
            }));
        if (status.Length > 0)
        {
            row.Add(TextBlock(status));
        }

        row.Add(container.State == ContainerState.Running
            ? Button(AppStrings.Get("StopButton"), () => _ = Props.Service.StopAsync(container))
            : Button(AppStrings.Get("StartButton"), () => _ = Props.Service.StartAsync(container)));
        row.Add(container.State == ContainerState.Running
            ? Button(AppStrings.Get("RestartButton"), () => _ = Props.Service.RestartAsync(container))
            : TextBlock(string.Empty));
        row.Add(container.State == ContainerState.Running
            ? Button(AppStrings.Get("OpenShellButton"), () => Props.OnOpenShell(container))
            : TextBlock(string.Empty));
        row.Add(container.State == ContainerState.Running
            ? Button(AppStrings.Get("OpenLogsButton"), () => Props.OnOpenLogs(container))
            : TextBlock(string.Empty));
        row.Add(Props.ConfirmRemove
            ? VStack(2,
                TextBlock(AppStrings.Format("RemoveConfirmation", container.Name)),
                Button(AppStrings.Get("ConfirmRemoveButton"), () => _ = Props.Service.RemoveAsync(container)),
                Button(AppStrings.Get("CancelButton"), Props.CancelRemove))
            : Button(AppStrings.Get("RemoveButton"), Props.RequestRemove));
        return VStack(2, row.ToArray());
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
