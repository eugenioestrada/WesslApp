using Wessl.App.Services;

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
    public void MissingFileUsesAutomaticThemeAndSupportedSystemLanguage()
    {
        using var directory = new TemporaryDirectory();

        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));

        Assert.Equal(AppTheme.Automatic, settings.Theme);
        Assert.True(settings.Language is AppLanguage.English or AppLanguage.Spanish);
    }

    [Fact]
    public void RejectsUnsupportedThemeAndLanguageValues()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings(Path.Combine(directory.Path, "settings.json"));

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetTheme((AppTheme)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetLanguage((AppLanguage)99));
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
