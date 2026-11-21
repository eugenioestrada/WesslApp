using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class ImagePage
{
    public static Element Create(ImageService service, bool confirmPrune, Action requestPrune, Action cancelPrune, Action back) =>
        ScrollViewer(VStack(12,
            HStack(12,
                TextBlock(AppStrings.Get("ImagesHeading")).FontSize(24),
                Button(AppStrings.Get("SettingsBackButton"), () => { cancelPrune(); back(); })),
            service.Error is { } error
                ? TextBlock(AppStrings.Format(error)).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap)
                : TextBlock(string.Empty),
            service.Status is { } status
                ? TextBlock(AppStrings.Format(status)).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap)
                : TextBlock(string.Empty),
            service.HasLoaded && service.Images.Count == 0
                ? TextBlock(AppStrings.Get("ImagesEmpty"))
                : VStack(8, service.Images.Select(image =>
                    VStack(2,
                        TextBlock(AppStrings.Format("ImageName", image.Repository, image.Tag)).SemiBold(),
                        TextBlock(AppStrings.Format("ImageDetails", image.Id, image.Size))).WithKey($"{image.Id}:{image.Repository}:{image.Tag}")
                    ).ToArray()),
            confirmPrune
                ? VStack(4,
                    TextBlock(AppStrings.Get("ImagesPruneConfirmation")).TextWrapping(Microsoft.UI.Xaml.TextWrapping.Wrap),
                    Button(AppStrings.Get("ImagesPruneConfirm"), () => { cancelPrune(); _ = service.PruneAsync(); }).IsEnabled(!service.IsBusy),
                    Button(AppStrings.Get("CancelButton"), cancelPrune))
                : Button(AppStrings.Get("ImagesPruneButton"), requestPrune).IsEnabled(!service.IsBusy),
            Button(AppStrings.Get("RefreshButton"), () => _ = service.RefreshAsync()).IsEnabled(!service.IsBusy)
        ).Padding(20));
}
