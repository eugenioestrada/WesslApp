using System.Globalization;
using Wessl.App.Services;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void ThemeAndLanguagePersistAcrossInstances()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        var settings = new AppSettings(path);
        settings.SetTheme(AppTheme.Dark);
        settings.SetLanguage(AppLanguage.Spanish);

        var reloaded = new AppSettings(path);

        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.Equal(AppLanguage.Spanish, reloaded.Language);
        Assert.Equal("es-ES", reloaded.LanguageTag);
    }

    [Fact]
    public void SimplifiedChineseLanguagePersistsAcrossInstances()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        var settings = new AppSettings(path);
        settings.SetLanguage(AppLanguage.ChineseSimplified);

        var reloaded = new AppSettings(path);

        Assert.Equal(AppLanguage.ChineseSimplified, reloaded.Language);
        Assert.Equal("zh-CN", reloaded.LanguageTag);
    }

    [Fact]
    public void SimplifiedChineseSystemCultureSelectsSimplifiedChinese()
    {
        using var directory = new TemporaryDirectory();
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));

            Assert.Equal(AppLanguage.ChineseSimplified, settings.Language);
            Assert.Equal("zh-CN", settings.LanguageTag);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(AppLanguage.French, "fr-FR", "fr-FR")]
    [InlineData(AppLanguage.Italian, "it-IT", "it-IT")]
    [InlineData(AppLanguage.German, "de-DE", "de-DE")]
    public void LanguagePersistsAndIsSelectedForSystemCulture(
        AppLanguage language,
        string cultureName,
        string languageTag)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var settings = new AppSettings(path);
            Assert.Equal(language, settings.Language);

            settings.SetLanguage(language);
            var reloaded = new AppSettings(path);

            Assert.Equal(language, reloaded.Language);
            Assert.Equal(languageTag, reloaded.LanguageTag);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void StartupRefreshIntervalAndTerminalPersistAcrossInstances()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        var settings = new AppSettings(path);
        settings.SetStartWithWindows(true);
        settings.SetRefreshIntervalSeconds(30);
        settings.SetDefaultTerminal(ContainerTerminal.PowerShell);

        var reloaded = new AppSettings(path);

        Assert.True(reloaded.StartWithWindows);
        Assert.Equal(30, reloaded.RefreshIntervalSeconds);
        Assert.Equal(ContainerTerminal.PowerShell, reloaded.DefaultTerminal);
    }

    [Fact]
    public void ChangingLanguagePreservesOtherSettingsSelections()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));
        settings.SetTheme(AppTheme.Dark);
        settings.SetRefreshIntervalSeconds(30);
        settings.SetDefaultTerminal(ContainerTerminal.PowerShell);

        settings.SetLanguage(AppLanguage.Spanish);

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(30, settings.RefreshIntervalSeconds);
        Assert.Equal(ContainerTerminal.PowerShell, settings.DefaultTerminal);
    }

    [Theory]
    [InlineData(ContainerTerminal.WindowsTerminal)]
    [InlineData(ContainerTerminal.PowerShell)]
    [InlineData(ContainerTerminal.CommandPrompt)]
    public void DefaultTerminalPreferencePersistsAcrossInstances(ContainerTerminal terminal)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        var settings = new AppSettings(path);
        settings.SetDefaultTerminal(terminal);

        var reloaded = new AppSettings(path);

        Assert.Equal(terminal, reloaded.DefaultTerminal);
    }

    [Fact]
    public void MissingFileUsesAutomaticThemeAndSupportedSystemLanguage()
    {
        using var directory = new TemporaryDirectory();

        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));

        Assert.Equal(AppTheme.Automatic, settings.Theme);
        Assert.True(settings.Language is AppLanguage.English
            or AppLanguage.Spanish
            or AppLanguage.ChineseSimplified
            or AppLanguage.French
            or AppLanguage.Italian
            or AppLanguage.German);
        Assert.False(settings.StartWithWindows);
        Assert.Equal(10, settings.RefreshIntervalSeconds);
        Assert.Equal(ContainerTerminal.WindowsTerminal, settings.DefaultTerminal);
    }

    [Fact]
    public void RejectsUnsupportedThemeAndLanguageValues()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetTheme((AppTheme)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetLanguage((AppLanguage)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetDefaultTerminal((ContainerTerminal)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetRefreshIntervalSeconds(15));
    }

    [Fact]
    public void ExistingSettingsWithoutNewOptionsUseDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(path, """{"Theme":1,"Language":0}""");

        var settings = new AppSettings(path);

        Assert.False(settings.StartWithWindows);
        Assert.Equal(10, settings.RefreshIntervalSeconds);
        Assert.Equal(ContainerTerminal.WindowsTerminal, settings.DefaultTerminal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void StatsIntervalPersistsAndSurvivesChangesToOtherOptions(int seconds)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings(path);
        var changes = 0;
        settings.Changed += () => changes++;

        Assert.Equal(1, settings.StatsIntervalSeconds);
        settings.SetStatsIntervalSeconds(seconds);
        settings.SetStatsIntervalSeconds(seconds);
        Assert.Equal(seconds == 1 ? 0 : 1, changes);
        settings.SetTheme(AppTheme.Dark);
        settings.SetRefreshIntervalSeconds(30);

        var reloaded = new AppSettings(path);
        Assert.Equal(seconds, reloaded.StatsIntervalSeconds);
        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.Equal(30, reloaded.RefreshIntervalSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(61)]
    public void UnsupportedStatsIntervalIsRejectedWithoutChangingSavedSettings(int seconds)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings(path);
        settings.SetStatsIntervalSeconds(5);
        var changes = 0;
        settings.Changed += () => changes++;

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetStatsIntervalSeconds(seconds));
        Assert.Equal(5, settings.StatsIntervalSeconds);
        Assert.Equal(5, new AppSettings(path).StatsIntervalSeconds);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void OldSettingsWithoutStatsIntervalKeepTheirValuesAndDefaultToOneSecond()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(path, """{"Theme":2,"Language":1,"StartWithWindows":true,"RefreshIntervalSeconds":30,"DefaultTerminal":1}""");

        var settings = new AppSettings(path);
        Assert.Equal(1, settings.StatsIntervalSeconds);
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(AppLanguage.Spanish, settings.Language);
        Assert.True(settings.StartWithWindows);
        Assert.Equal(30, settings.RefreshIntervalSeconds);
        Assert.Equal(ContainerTerminal.PowerShell, settings.DefaultTerminal);
        settings.SetStatsIntervalSeconds(10);

        var reloaded = new AppSettings(path);
        Assert.Equal(10, reloaded.StatsIntervalSeconds);
        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.Equal(ContainerTerminal.PowerShell, reloaded.DefaultTerminal);
    }

    [Fact]
    public void UnsupportedStoredStatsIntervalIsRejected()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(path, """{"Theme":0,"Language":0,"StatsIntervalSeconds":15}""");

        Assert.Throws<InvalidDataException>(() => new AppSettings(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "WesslSettingsTests",
            Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
