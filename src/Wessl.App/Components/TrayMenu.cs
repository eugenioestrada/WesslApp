using Microsoft.UI.Xaml.Controls;

namespace Wessl.App.Components;

public static class TrayMenu
{
    public static MenuFlyout Create(Action open, Action exit)
    {
        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Open" };
        var exitItem = new MenuFlyoutItem { Text = "Exit" };

        openItem.Click += (_, _) => open();
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(openItem);
        menu.Items.Add(exitItem);

        return menu;
    }
}
