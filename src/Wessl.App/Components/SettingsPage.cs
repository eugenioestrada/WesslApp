using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Wessl.App.Services;
using Wessl.Domain;
using static Microsoft.UI.Reactor.Factories;

namespace Wessl.App.Components;

public static class SettingsPage
{
    public static Element Create(AppSettings settings, Action navigateBack) =>
        ScrollViewer(VStack(12,
            TextBlock(AppStrings.Get("SettingsHeading")).FontSize(24),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsThemeLabel")),
                RadioButtons(
                    [
                        AppStrings.Get("SettingsThemeAutomatic"),
                        AppStrings.Get("SettingsThemeLight"),
                        AppStrings.Get("SettingsThemeDark")
                    ],
                    (int)settings.Theme,
                    index => { if (index >= 0) settings.SetTheme((AppTheme)index); })
                    .AutomationName(AppStrings.Get("SettingsThemeLabel"))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsLanguageLabel")),
                ComboBox(
                    [
                        AppStrings.Get("SettingsLanguageEnglish"),
                        AppStrings.Get("SettingsLanguageSpanish")
                    ],
                    (int)settings.Language,
                    index => { if (index >= 0) settings.SetLanguage((AppLanguage)index); })
                    .AutomationName(AppStrings.Get("SettingsLanguageLabel"))),
            ToggleSwitch(
                settings.StartWithWindows,
                enabled => settings.SetStartWithWindows(enabled),
                header: AppStrings.Get("SettingsStartupLabel"),
                onContent: AppStrings.Get("SettingsStartupEnabled"),
                offContent: AppStrings.Get("SettingsStartupDisabled")),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsRefreshIntervalLabel")),
                RadioButtons(
                    [
                        AppStrings.Get("SettingsRefreshInterval5"),
                        AppStrings.Get("SettingsRefreshInterval10"),
                        AppStrings.Get("SettingsRefreshInterval30"),
                        AppStrings.Get("SettingsRefreshInterval60")
                    ],
                    RefreshIntervalIndex(settings.RefreshIntervalSeconds),
                    index => { if (index >= 0) settings.SetRefreshIntervalSeconds(new[] { 5, 10, 30, 60 }[index]); })
                    .AutomationName(AppStrings.Get("SettingsRefreshIntervalLabel"))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsDefaultTerminalLabel")),
                RadioButtons(
                    [
                        AppStrings.Get("SettingsTerminalWindowsTerminal"),
                        AppStrings.Get("SettingsTerminalPowerShell"),
                        AppStrings.Get("SettingsTerminalCommandPrompt")
                    ],
                    (int)settings.DefaultTerminal,
                    index => { if (index >= 0) settings.SetDefaultTerminal((ContainerTerminal)index); })
                    .AutomationName(AppStrings.Get("SettingsDefaultTerminalLabel"))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsContainerClientLabel")),
                TextBlock(AppStrings.Get("SettingsContainerClientValue"))),
            Button(AppStrings.Get("SettingsBackButton"), navigateBack)
        ).Padding(20));

    private static int RefreshIntervalIndex(int seconds) => seconds switch
    {
        5 => 0,
        10 => 1,
        30 => 2,
        60 => 3,
        _ => 1
    };
}
