using Microsoft.UI.Xaml.Controls;
using Wessl.App.Services;

namespace Wessl.App.Components;

public static class TrayMenu
{
    public static MenuFlyout Create(Action open, Action settings, Action exit)
    {
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        var openItem = new MenuFlyoutItem { Text = AppStrings.Get("TrayOpen") };
        var settingsItem = new MenuFlyoutItem { Text = AppStrings.Get("SettingsOpenButton") };
        var exitItem = new MenuFlyoutItem { Text = AppStrings.Get("TrayExit") };

        openItem.Click += (_, _) => open();
        settingsItem.Click += (_, _) => settings();
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(openItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(exitItem);

        return menu;
    }
}
