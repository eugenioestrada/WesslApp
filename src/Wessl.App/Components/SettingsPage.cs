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
                HStack(6,
                    ThemeButton("SettingsThemeAutomatic", "SettingsThemeAutomaticSelected", AppTheme.Automatic, settings),
                    ThemeButton("SettingsThemeLight", "SettingsThemeLightSelected", AppTheme.Light, settings),
                    ThemeButton("SettingsThemeDark", "SettingsThemeDarkSelected", AppTheme.Dark, settings))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsLanguageLabel")),
                HStack(6,
                    LanguageButton("SettingsLanguageEnglish", "SettingsLanguageEnglishSelected", AppLanguage.English, settings),
                    LanguageButton("SettingsLanguageSpanish", "SettingsLanguageSpanishSelected", AppLanguage.Spanish, settings))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsStartupLabel")),
                Button(
                    AppStrings.Get(settings.StartWithWindows ? "SettingsStartupEnabled" : "SettingsStartupDisabled"),
                    () => settings.SetStartWithWindows(!settings.StartWithWindows))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsRefreshIntervalLabel")),
                HStack(6,
                    RefreshIntervalButton(5, settings),
                    RefreshIntervalButton(10, settings),
                    RefreshIntervalButton(30, settings),
                    RefreshIntervalButton(60, settings))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsDefaultTerminalLabel")),
                VStack(2,
                    TerminalButton("SettingsTerminalWindowsTerminal", "SettingsTerminalWindowsTerminalSelected", ContainerTerminal.WindowsTerminal, settings),
                    TerminalButton("SettingsTerminalPowerShell", "SettingsTerminalPowerShellSelected", ContainerTerminal.PowerShell, settings),
                    TerminalButton("SettingsTerminalCommandPrompt", "SettingsTerminalCommandPromptSelected", ContainerTerminal.CommandPrompt, settings))),
            VStack(4,
                TextBlock(AppStrings.Get("SettingsContainerClientLabel")),
                TextBlock(AppStrings.Get("SettingsContainerClientValue"))),
            Button(AppStrings.Get("SettingsBackButton"), navigateBack)
        ).Padding(20));

    private static Element ThemeButton(string labelKey, string selectedLabelKey, AppTheme theme, AppSettings settings) =>
        Button(AppStrings.Get(settings.Theme == theme ? selectedLabelKey : labelKey), () => settings.SetTheme(theme));

    private static Element LanguageButton(string labelKey, string selectedLabelKey, AppLanguage language, AppSettings settings) =>
        Button(AppStrings.Get(settings.Language == language ? selectedLabelKey : labelKey), () => settings.SetLanguage(language));

    private static Element RefreshIntervalButton(int seconds, AppSettings settings)
    {
        var labelKey = $"SettingsRefreshInterval{seconds}";
        var selectedLabelKey = $"{labelKey}Selected";
        return Button(
            AppStrings.Get(settings.RefreshIntervalSeconds == seconds ? selectedLabelKey : labelKey),
            () => settings.SetRefreshIntervalSeconds(seconds));
    }

    private static Element TerminalButton(
        string labelKey,
        string selectedLabelKey,
        ContainerTerminal terminal,
        AppSettings settings) =>
        Button(
            AppStrings.Get(settings.DefaultTerminal == terminal ? selectedLabelKey : labelKey),
            () => settings.SetDefaultTerminal(terminal));
}
