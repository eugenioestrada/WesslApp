using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Wessl.App.Services;
using Wessl.Domain;
using Windows.ApplicationModel.DataTransfer;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public sealed record ContainerDetailsViewProps(
    ContainerInfo Container,
    ContainerDetailsService Service);

public sealed class ContainerDetailsView : Component<ContainerDetailsViewProps>
{
    public static Element Create(ContainerInfo container, ContainerDetailsService service) =>
        Component<ContainerDetailsView, ContainerDetailsViewProps>(
            new(container, service));

    public override Element Render()
    {
        var (copyStatus, setCopyStatus) = UseState<LocalizedMessage?>(null);
        var isCurrentContainer = Props.Service.Container?.Id == Props.Container.Id;
        var details = isCurrentContainer ? Props.Service.Details : null;
        var content = new List<Element>
        {
            InfoBar(message: AppStrings.Get("ContainerDetailsEnvironmentOmitted"))
                .Informational() with { IsOpen = true, IsClosable = false }
        };

        if (!isCurrentContainer || Props.Service.IsLoading)
        {
            content.Add(InfoBar(message: AppStrings.Get("ContainerDetailsLoading"))
                .Informational() with { IsOpen = true, IsClosable = false });
            content.Add(ProgressRing());
        }
        else if (Props.Service.Error is { } error)
        {
            content.Add(InfoBar(message: AppStrings.Format("ContainerDetailsError", error.Message))
                .Error() with { IsOpen = true, IsClosable = false });
        }
        else if (details is not null)
        {
            content.AddRange(new Element[]
            {
                DetailField("ContainerDetailsImage", details.Image),
                DetailField("ContainerDetailsImageDigest", details.ImageDigest),
                DetailField("ContainerDetailsCreated", FormatDate(details.CreatedAt)),
                DetailField("ContainerDetailsStarted", FormatDate(details.StartedAt)),
                DetailField("ContainerDetailsCommand", FormatCommand(details.Command)),
                DetailField("ContainerDetailsPorts", FormatList(details.Ports)),
                DetailField("ContainerDetailsMounts", FormatList(details.Mounts)),
                DetailField("ContainerDetailsNetworks", FormatList(details.Networks))
            });
        }
        else
        {
            content.Add(TextBlock(AppStrings.Get("ContainerDetailsUnavailable"))
                .TextWrapping(TextWrapping.Wrap));
        }

        var actions = new List<Element>();
        if (details is not null)
        {
            actions.Add(Button(AppStrings.Get("ContainerDetailsCopyButton"), () => CopyDetails(details, setCopyStatus)));
        }

        var rows = copyStatus is null
            ? new[] { GridSize.Star().MinSize(0), GridSize.Auto }
            : new[] { GridSize.Star().MinSize(0), GridSize.Auto, GridSize.Auto };
        var elements = new List<Element>
        {
            (ScrollViewer(VStack(8, content.ToArray()))
                .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Disabled)
                .VerticalScrollMode(Microsoft.UI.Xaml.Controls.ScrollMode.Enabled) with
                {
                    HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto
                }).Grid(row: 0)
        };
        var actionRow = 1;
        if (copyStatus is { } status)
        {
            var message = InfoBar(message: AppStrings.Format(status));
            elements.Add(((status.ResourceKey == "ContainerDetailsCopyError" ? message.Error() : message.Success())
                with { IsOpen = true, IsClosable = false }).Grid(row: 1));
            actionRow = 2;
        }

        elements.Add(HStack(8, actions.ToArray()).Grid(row: actionRow));
        return Grid([GridSize.Star().MinSize(0)], rows, elements.ToArray());
    }

    private static Element DetailField(string labelKey, string? value) =>
        VStack(4,
            TextBlock(AppStrings.Get(labelKey)).SemiBold(),
            TextBlock(string.IsNullOrWhiteSpace(value)
                ? AppStrings.Get("ContainerDetailsUnavailable")
                : value).TextWrapping(TextWrapping.Wrap));

    private static string? FormatList(IReadOnlyList<string>? values) =>
        values is null ? null : values.Count == 0 ? AppStrings.Get("ContainerDetailsNone") : string.Join(Environment.NewLine, values);

    private static string? FormatCommand(string? command) =>
        command == "[]" ? AppStrings.Get("ContainerDetailsNone") : command;

    private static string? FormatDate(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? $"{timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} {timestamp.ToLocalTime().ToString("zzz", CultureInfo.CurrentCulture)}"
            : value;

    private static void CopyDetails(ContainerDetails details, Action<LocalizedMessage?> setStatus)
    {
        var lines = new[]
        {
            CopyField("ContainerDetailsImage", details.Image),
            CopyField("ContainerDetailsImageDigest", details.ImageDigest),
            CopyField("ContainerDetailsCreated", details.CreatedAt),
            CopyField("ContainerDetailsStarted", details.StartedAt),
            CopyField("ContainerDetailsCommand", FormatCommand(details.Command)),
            CopyField("ContainerDetailsPorts", FormatList(details.Ports)),
            CopyField("ContainerDetailsMounts", FormatList(details.Mounts)),
            CopyField("ContainerDetailsNetworks", FormatList(details.Networks))
        };

        try
        {
            var package = new DataPackage();
            package.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(package);
            setStatus(new("ContainerDetailsCopied", []));
        }
        catch (Exception exception)
        {
            setStatus(new("ContainerDetailsCopyError", [exception.Message]));
        }
    }

    private static string CopyField(string labelKey, string? value) =>
        $"{AppStrings.Get(labelKey)}: {value ?? AppStrings.Get("ContainerDetailsUnavailable")}";
}
