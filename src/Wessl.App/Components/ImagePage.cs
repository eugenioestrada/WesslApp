using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
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
        Action back) =>
        ScrollViewer(VStack(12,
            HStack(12,
                TextBlock(AppStrings.Get("ImagesHeading")).FontSize(24),
                Button(AppStrings.Get("SettingsBackButton"), () => { cancelPrune(); back(); })),
            service.Error is { } error
                ? InfoBar(AppStrings.Format(error)).Error() with { IsOpen = true, IsClosable = false }
                : TextBlock(string.Empty),
            service.Status is { } status
                ? (status.ResourceKey.EndsWith("Pending", System.StringComparison.Ordinal)
                    || status.ResourceKey == "ImagesLoading"
                    ? InfoBar(AppStrings.Format(status)).Informational()
                    : InfoBar(AppStrings.Format(status)).Success())
                    with { IsOpen = true, IsClosable = false }
                : TextBlock(string.Empty),
            !service.HasLoaded && service.IsBusy
                ? VStack(8,
                    TextBlock(AppStrings.Get("ImagesLoading")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                    ProgressRing())
                : service.HasLoaded && service.Images.Count == 0
                ? TextBlock(AppStrings.Get("ImagesEmpty"))
                : VStack(8, service.Images.Select(image =>
                    VStack(2,
                        TextBlock(AppStrings.Format("ImageName", image.Repository, image.Tag)).SemiBold(),
                        TextBlock(AppStrings.Format("ImageDetails", image.Id, image.Size)),
                        !string.IsNullOrWhiteSpace(image.Digest) && image.Digest != "<none>"
                            ? TextBlock(AppStrings.Format("ImageDigest", image.Digest))
                            : TextBlock(string.Empty)).WithKey($"{image.Id}:{image.Repository}:{image.Tag}")
                    ).ToArray()),
            VStack(4,
                TextBlock(AppStrings.Get("ImagesPullHeading")),
                TextBlock(AppStrings.Get("ImagesPullReferenceLabel")),
                TextBox(pullReference, setPullReference)
                    .PlaceholderText(AppStrings.Get("ImagesPullReferencePlaceholder"))
                    .AutomationName(AppStrings.Get("ImagesPullReferenceLabel"))
                    .IsEnabled(!service.IsBusy),
                Button(AppStrings.Get("ImagesPullButton"), () => _ = service.PullImageAsync(pullReference))
                    .IsEnabled(!service.IsBusy && !string.IsNullOrWhiteSpace(pullReference))),
            VStack(4,
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
                        && !string.IsNullOrWhiteSpace(tagTarget))),
            confirmPrune
                ? VStack(4,
                    TextBlock(AppStrings.Get("ImagesPruneConfirmation")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                    Button(AppStrings.Get("ImagesPruneConfirm"), () => { cancelPrune(); _ = service.PruneAsync(); }).IsEnabled(!service.IsBusy),
                    Button(AppStrings.Get("CancelButton"), cancelPrune))
                : Button(AppStrings.Get("ImagesPruneButton"), requestPrune).IsEnabled(!service.IsBusy),
            Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync()).IsEnabled(!service.IsBusy)
        ).Padding(20));
}
