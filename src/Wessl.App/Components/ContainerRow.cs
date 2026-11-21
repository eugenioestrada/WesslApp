using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed class ContainerRow : Component<ContainerInfo>
{
    public static Element Create(ContainerInfo container) =>
        Component<ContainerRow, ContainerInfo>(container);

    public override Element Render()
    {
        var (status, setStatus) = UseState(string.Empty);
        var ports = Props.Ports
            .Where(port => port.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            .Select(port =>
            {
                var url = $"http://localhost:{port.HostPort}";
                return HStack(8,
                    Button(url, () => _ = OpenUrlAsync(url, setStatus)),
                    Button("Copy URL", () => CopyUrl(url, setStatus)));
            })
            .ToArray();

        var row = new List<Element>
        {
            TextBlock(Props.Name).SemiBold(),
            TextBlock($"{Props.Image} · {Props.State}")
        };
        row.AddRange(ports);
        if (status.Length > 0)
        {
            row.Add(TextBlock(status));
        }

        return VStack(4, row.ToArray());
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
