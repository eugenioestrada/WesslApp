using Microsoft.UI.Xaml.Controls;
using Wessl.App.Services;

namespace Wessl.App.Components;

public static class TrayMenu
{
    public static MenuFlyout Create(Action open, Action exit)
    {
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        var openItem = new MenuFlyoutItem { Text = AppStrings.Get("TrayOpen") };
        var exitItem = new MenuFlyoutItem { Text = AppStrings.Get("TrayExit") };

        openItem.Click += (_, _) => open();
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(openItem);
        menu.Items.Add(exitItem);

        return menu;
    }
}
