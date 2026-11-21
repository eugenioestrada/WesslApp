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
        Assert.True(settings.Language is AppLanguage.English or AppLanguage.Spanish);
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
