using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ImagePage
{
    public static Element Create(
        ImageService service,
        bool confirmPrune,
        Action requestPrune,
        Action cancelPrune,
        string pullReference,
        Action<string> setPullReference,
        string tagSource,
        Action<string> setTagSource,
        string tagTarget,
        Action<string> setTagTarget,
        Action back)
    {
        var sections = new List<Element>();
        if (service.Error is { } error)
        {
            sections.Add(InfoBar(message: AppStrings.Format(error))
                .Error() with { IsOpen = true, IsClosable = false });
        }
        if (service.Status is { } status && status.ResourceKey != "ImagesLoading")
        {
            var message = InfoBar(message: AppStrings.Format(status));
            sections.Add((status.ResourceKey.EndsWith("Pending", StringComparison.Ordinal)
                    ? message.Informational()
                    : message.Success())
                with { IsOpen = true, IsClosable = false });
        }
        if (service.HasLoaded && (service.IsBusy || service.Error is not null))
        {
            sections.Add(InfoBar(message: AppStrings.Get("ImagesShowingPreviousData"))
                .Informational() with { IsOpen = true, IsClosable = false });
        }

        if (!service.HasLoaded && service.IsBusy)
        {
            sections.Add(VStack(8,
                TextBlock(AppStrings.Get("ImagesLoading")).TextWrapping(TextWrapping.Wrap),
                ProgressRing()));
        }
        else if (!service.HasLoaded && service.Error is not null)
        {
            sections.Add(TextBlock(AppStrings.Get("ImagesLoadFailed"))
                .TextWrapping(TextWrapping.Wrap));
            sections.Add(Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync())
                .IsEnabled(!service.IsBusy));
        }
        else if (service.HasLoaded && service.Images.Count == 0)
        {
            sections.Add(TextBlock(AppStrings.Get("ImagesEmpty"))
                .TextWrapping(TextWrapping.Wrap));
        }
        else if (service.Images.Count > 0)
        {
            sections.Add(VStack(8, service.Images.Select(CreateImageCard).ToArray()));
        }

        sections.Add(VStack(4,
            TextBlock(AppStrings.Get("ImagesPullHeading")),
            TextBlock(AppStrings.Get("ImagesPullReferenceLabel")),
            TextBox(pullReference, setPullReference)
                .PlaceholderText(AppStrings.Get("ImagesPullReferencePlaceholder"))
                .AutomationName(AppStrings.Get("ImagesPullReferenceLabel"))
                .IsEnabled(!service.IsBusy),
            Button(AppStrings.Get("ImagesPullButton"), () => _ = service.PullImageAsync(pullReference))
                .IsEnabled(!service.IsBusy && !string.IsNullOrWhiteSpace(pullReference))));
        sections.Add(VStack(4,
            TextBlock(AppStrings.Get("ImagesTagHeading")),
            TextBlock(AppStrings.Get("ImagesTagSourceLabel")),
            TextBox(tagSource, setTagSource)
                .PlaceholderText(AppStrings.Get("ImagesTagSourcePlaceholder"))
                .AutomationName(AppStrings.Get("ImagesTagSourceLabel"))
                .IsEnabled(!service.IsBusy),
            TextBlock(AppStrings.Get("ImagesTagTargetLabel")),
            TextBox(tagTarget, setTagTarget)
                .PlaceholderText(AppStrings.Get("ImagesTagTargetPlaceholder"))
                .AutomationName(AppStrings.Get("ImagesTagTargetLabel"))
                .IsEnabled(!service.IsBusy),
            Button(AppStrings.Get("ImagesTagButton"), () => _ = service.TagImageAsync(tagSource, tagTarget))
                .IsEnabled(!service.IsBusy
                    && !string.IsNullOrWhiteSpace(tagSource)
                    && !string.IsNullOrWhiteSpace(tagTarget))));
        sections.Add(Button(AppStrings.Get("ImagesPruneButton"), requestPrune)
            .IsEnabled(!service.IsBusy));
        if (service.HasLoaded || service.Error is null)
        {
            sections.Add(Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync())
                .IsEnabled(!service.IsBusy));
        }

        var page = Grid(
            [GridSize.Star().MinSize(0)],
            [GridSize.Auto, GridSize.Star().MinSize(0)],
            PageHeader.Create(
                AppStrings.Get("ImagesHeading"),
                () => { cancelPrune(); back(); }).Grid(row: 0),
            (ScrollViewer(Grid(
                    [GridSize.Star().MinSize(0)],
                    [GridSize.Star().MinSize(0)],
                    VStack(12, sections.ToArray()).Grid(row: 0)))
                .HorizontalScrollMode(ScrollMode.Disabled)
                with
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                }).Grid(row: 1))
            .Padding(16);

        var pruneDialog = (ContentDialog(
            AppStrings.Get("ImagesPruneConfirmationTitle"),
            TextBlock(AppStrings.Get("ImagesPruneConfirmation"))
                .TextWrapping(TextWrapping.Wrap),
            AppStrings.Get("ImagesPruneConfirm")) with
            {
                IsOpen = confirmPrune,
                CloseButtonText = AppStrings.Get("CancelButton"),
                DefaultButton = ContentDialogButton.Close
            })
            .Closed(result =>
            {
                cancelPrune();
                if (result == ContentDialogResult.Primary)
                {
                    _ = service.PruneAsync();
                }
            });

        return Grid([GridSize.Star()], [GridSize.Star()], page, pruneDialog);
    }

    private static Element CreateImageCard(ImageInfo image)
    {
        var fields = new List<Element>
        {
            TextBlock(AppStrings.Format("ImageName", image.Repository, image.Tag))
                .SemiBold()
                .TextWrapping(TextWrapping.Wrap),
            TextBlock(AppStrings.Format("ImageDetails", image.Id, image.Size))
                .TextWrapping(TextWrapping.Wrap)
        };
        if (!string.IsNullOrWhiteSpace(image.Digest) && image.Digest != "<none>")
        {
            fields.Add(TextBlock(AppStrings.Format("ImageDigest", image.Digest))
                .TextWrapping(TextWrapping.Wrap)
                .ToolTip(image.Digest));
        }

        return Card(Grid(
                [GridSize.Star().MinSize(0)],
                [GridSize.Auto],
                VStack(4, fields.ToArray()).Grid(row: 0)))
            .Padding(12)
            .WithKey($"{image.Id}:{image.Repository}:{image.Tag}");
    }
}
