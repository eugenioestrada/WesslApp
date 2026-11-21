using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wessl.Domain;

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
        RefreshIntervalSeconds = 10;
        DefaultTerminal = ContainerTerminal.WindowsTerminal;

        if (!File.Exists(_filePath))
        {
            return;
        }

        var stored = JsonSerializer.Deserialize<StoredSettings>(
            File.ReadAllText(_filePath),
            AppSettingsJsonContext.Default.StoredSettings)
            ?? throw new InvalidDataException("The Wessl settings file is empty.");
        if (!Enum.IsDefined(stored.Theme)
            || !Enum.IsDefined(stored.Language)
            || !Enum.IsDefined(stored.DefaultTerminal)
            || !IsSupportedRefreshInterval(stored.RefreshIntervalSeconds))
        {
            throw new InvalidDataException("The Wessl settings file contains an unsupported setting.");
        }

        Theme = stored.Theme;
        Language = stored.Language;
        StartWithWindows = stored.StartWithWindows;
        RefreshIntervalSeconds = stored.RefreshIntervalSeconds;
        DefaultTerminal = stored.DefaultTerminal;
    }

    public event Action? Changed;

    public AppTheme Theme { get; private set; }

    public AppLanguage Language { get; private set; }

    public bool StartWithWindows { get; private set; }

    public int RefreshIntervalSeconds { get; private set; }

    public ContainerTerminal DefaultTerminal { get; private set; }

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

        Save(theme, Language, StartWithWindows, RefreshIntervalSeconds, DefaultTerminal);
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

        Save(Theme, language, StartWithWindows, RefreshIntervalSeconds, DefaultTerminal);
        Language = language;
        Changed?.Invoke();
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (StartWithWindows == enabled)
        {
            return;
        }

        Save(Theme, Language, enabled, RefreshIntervalSeconds, DefaultTerminal);
        StartWithWindows = enabled;
        Changed?.Invoke();
    }

    public void SetRefreshIntervalSeconds(int seconds)
    {
        if (!IsSupportedRefreshInterval(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (RefreshIntervalSeconds == seconds)
        {
            return;
        }

        Save(Theme, Language, StartWithWindows, seconds, DefaultTerminal);
        RefreshIntervalSeconds = seconds;
        Changed?.Invoke();
    }

    public void SetDefaultTerminal(ContainerTerminal terminal)
    {
        if (!Enum.IsDefined(terminal))
        {
            throw new ArgumentOutOfRangeException(nameof(terminal));
        }

        if (DefaultTerminal == terminal)
        {
            return;
        }

        Save(Theme, Language, StartWithWindows, RefreshIntervalSeconds, terminal);
        DefaultTerminal = terminal;
        Changed?.Invoke();
    }

    public static bool IsSupportedRefreshInterval(int seconds) =>
        seconds is 5 or 10 or 30 or 60;

    private void Save(
        AppTheme theme,
        AppLanguage language,
        bool startWithWindows,
        int refreshIntervalSeconds,
        ContainerTerminal defaultTerminal)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The settings file path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            _filePath,
            JsonSerializer.Serialize(
                new StoredSettings(
                    theme,
                    language,
                    startWithWindows,
                    refreshIntervalSeconds,
                    defaultTerminal),
                AppSettingsJsonContext.Default.StoredSettings));
    }

    internal sealed record StoredSettings(
        AppTheme Theme,
        AppLanguage Language,
        bool StartWithWindows = false,
        int RefreshIntervalSeconds = 10,
        ContainerTerminal DefaultTerminal = ContainerTerminal.WindowsTerminal);
}

[JsonSerializable(typeof(AppSettings.StoredSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}
