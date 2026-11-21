using System.Globalization;
using System.Text.Json;

namespace Wessl.App.Services;

public enum AppTheme
{
    Automatic,
    Light,
    Dark
}

public enum AppLanguage
{
    English,
    Spanish
}

public sealed class AppSettings
{
    private readonly string _filePath;

    public AppSettings(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Wessl",
            "settings.json");

        Theme = AppTheme.Automatic;
        Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Spanish
            : AppLanguage.English;

        if (!File.Exists(_filePath))
        {
            return;
        }

        var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(_filePath))
            ?? throw new InvalidDataException("The Wessl settings file is empty.");
        if (!Enum.IsDefined(stored.Theme) || !Enum.IsDefined(stored.Language))
        {
            throw new InvalidDataException("The Wessl settings file contains an unsupported setting.");
        }

        Theme = stored.Theme;
        Language = stored.Language;
    }

    public event Action? Changed;

    public AppTheme Theme { get; private set; }

    public AppLanguage Language { get; private set; }

    public string LanguageTag => Language switch
    {
        AppLanguage.English => "en-US",
        AppLanguage.Spanish => "es-ES",
        _ => throw new InvalidOperationException("The selected language is not supported.")
    };

    public void SetTheme(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        if (Theme == theme)
        {
            return;
        }

        Save(theme, Language);
        Theme = theme;
        Changed?.Invoke();
    }

    public void SetLanguage(AppLanguage language)
    {
        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language));
        }

        if (Language == language)
        {
            return;
        }

        Save(Theme, language);
        Language = language;
        Changed?.Invoke();
    }

    private void Save(AppTheme theme, AppLanguage language)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The settings file path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(new StoredSettings(theme, language)));
    }

    private sealed record StoredSettings(AppTheme Theme, AppLanguage Language);
}
