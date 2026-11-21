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
            // Collection-backed controls need a new identity when the locale changes.
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
                    .AutomationName(AppStrings.Get("SettingsThemeLabel"))
                    .WithKey($"settings-theme-{settings.LanguageTag}")),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsLanguageLabel")),
                ComboBox(
                    [
                        AppStrings.Get("SettingsLanguageEnglish"),
                        AppStrings.Get("SettingsLanguageSpanish"),
                        AppStrings.Get("SettingsLanguageChineseSimplified"),
                        AppStrings.Get("SettingsLanguageFrench"),
                        AppStrings.Get("SettingsLanguageItalian"),
                        AppStrings.Get("SettingsLanguageGerman")
                    ],
                    (int)settings.Language,
                    index => { if (index >= 0) settings.SetLanguage((AppLanguage)index); })
                    .AutomationName(AppStrings.Get("SettingsLanguageLabel"))
                    .WithKey($"settings-language-{settings.LanguageTag}")),
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
                    .AutomationName(AppStrings.Get("SettingsRefreshIntervalLabel"))
                    .WithKey($"settings-refresh-{settings.LanguageTag}")),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsStatsIntervalLabel")),
                RadioButtons(
                    [
                        AppStrings.Get("SettingsStatsInterval1"),
                        AppStrings.Get("SettingsRefreshInterval5"),
                        AppStrings.Get("SettingsRefreshInterval10"),
                        AppStrings.Get("SettingsRefreshInterval30"),
                        AppStrings.Get("SettingsRefreshInterval60")
                    ],
                    StatsIntervalIndex(settings.StatsIntervalSeconds),
                    index => { if (index >= 0) settings.SetStatsIntervalSeconds(new[] { 1, 5, 10, 30, 60 }[index]); })
                    .AutomationName(AppStrings.Get("SettingsStatsIntervalLabel"))
                    .WithKey($"settings-stats-{settings.LanguageTag}")),
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
                    .AutomationName(AppStrings.Get("SettingsDefaultTerminalLabel"))
                    .WithKey($"settings-terminal-{settings.LanguageTag}")),
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

    private static int StatsIntervalIndex(int seconds) => seconds switch
    {
        1 => 0,
        5 => 1,
        10 => 2,
        30 => 3,
        60 => 4,
        _ => throw new InvalidOperationException("The selected stats interval is not supported.")
    };
}
