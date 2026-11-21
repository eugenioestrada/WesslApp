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
        var ports = container.Ports.Count == 0 ? "None" : string.Join(", ", container.Ports);
        var row = new List<Element>
        {
            TextBlock(container.Name).SemiBold(),
            TextBlock($"{container.Image} · {container.State}"),
            TextBlock($"Ports: {ports}")
        };
        row.AddRange(container.PublishedPorts
            .Where(port => port.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            .Select(port =>
            {
                var url = $"http://localhost:{port.HostPort}";
                return HStack(8,
                    Button(url, () => _ = OpenUrlAsync(url, setStatus)),
                    Button("Copy URL", () => CopyUrl(url, setStatus)));
            }));
        if (status.Length > 0)
        {
            row.Add(TextBlock(status));
        }

        row.Add(container.State == ContainerState.Running
            ? Button("Stop", () => _ = Props.Service.StopAsync(container))
            : Button("Start", () => _ = Props.Service.StartAsync(container)));
        row.Add(container.State == ContainerState.Running
            ? Button("Restart", () => _ = Props.Service.RestartAsync(container))
            : TextBlock(string.Empty));
        row.Add(container.State == ContainerState.Running
            ? Button("Open shell", () => Props.OnOpenShell(container))
            : TextBlock(string.Empty));
        row.Add(Props.ConfirmRemove
            ? VStack(2,
                TextBlock($"Remove {container.Name}? This permanently deletes the container and stops it if running."),
                Button("Confirm remove", () => _ = Props.Service.RemoveAsync(container)),
                Button("Cancel", Props.CancelRemove))
            : Button("Remove", Props.RequestRemove));
        return VStack(2, row.ToArray());
    }

    private static async Task OpenUrlAsync(string url, Action<string> setStatus)
    {
        try
        {
            var launched = await Launcher.LaunchUriAsync(new Uri(url));
            setStatus(launched ? $"Opened {url}" : $"Windows could not open {url}.");
        }
        catch (Exception exception)
        {
            setStatus($"Unable to open {url}: {exception.Message}");
        }
    }

    private static void CopyUrl(string url, Action<string> setStatus)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(url);
            Clipboard.SetContent(package);
            setStatus($"Copied {url} to the clipboard.");
        }
        catch (Exception exception)
        {
            setStatus($"Unable to copy {url}: {exception.Message}");
        }
    }
}
