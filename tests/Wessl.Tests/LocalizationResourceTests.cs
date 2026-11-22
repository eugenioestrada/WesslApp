using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Wessl.Tests;

public sealed class LocalizationResourceTests
{
    private static readonly Regex PlaceholderPattern = new(@"\{(\d+)(?:[,}:])", RegexOptions.Compiled);

    [Fact]
    public void SupportedLanguagesHaveTheSameUniqueKeysAndFormatPlaceholders()
    {
        var resources = LoadResources();
        var english = resources["en-US"];
        var englishKeys = english.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var (language, values) in resources)
        {
            Assert.Equal(english.Count, values.Count);
            Assert.True(englishKeys.SetEquals(values.Keys), $"{language} has a different set of resource keys.");

            foreach (var key in english.Keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language}/{key} has no value.");
                Assert.Equal(
                    GetPlaceholders(english[key]),
                    GetPlaceholders(values[key]));
            }
        }
    }

    [Theory]
    [InlineData("en-US", "Dark", "Active refresh interval", "5 seconds", "10 seconds", "30 seconds", "60 seconds")]
    [InlineData("es-ES", "Oscuro", "Intervalo de actualización activa", "5 segundos", "10 segundos", "30 segundos", "60 segundos")]
    [InlineData("zh-CN", "深色", "活动刷新间隔", "5 秒", "10 秒", "30 秒", "60 秒")]
    [InlineData("fr-FR", "Sombre", "Intervalle d’actualisation active", "5 secondes", "10 secondes", "30 secondes", "60 secondes")]
    [InlineData("it-IT", "Scuro", "Intervallo di aggiornamento attivo", "5 secondi", "10 secondi", "30 secondi", "60 secondi")]
    [InlineData("de-DE", "Dunkel", "Aktives Aktualisierungsintervall", "5 Sekunden", "10 Sekunden", "30 Sekunden", "60 Sekunden")]
    public void ThemeAndRefreshSettingsHaveLocalizedValues(
        string language,
        string darkTheme,
        string refreshLabel,
        string fiveSeconds,
        string tenSeconds,
        string thirtySeconds,
        string sixtySeconds)
    {
        var resources = LoadResources()[language];

        Assert.Equal(darkTheme, resources["SettingsThemeDark"]);
        Assert.Equal(refreshLabel, resources["SettingsRefreshIntervalLabel"]);
        Assert.Equal(fiveSeconds, resources["SettingsRefreshInterval5"]);
        Assert.Equal(tenSeconds, resources["SettingsRefreshInterval10"]);
        Assert.Equal(thirtySeconds, resources["SettingsRefreshInterval30"]);
        Assert.Equal(sixtySeconds, resources["SettingsRefreshInterval60"]);
    }

    [Fact]
    public void SettingsChoiceControlsAreRecreatedWhenLanguageChanges()
    {
        var settingsPage = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Wessl.App",
            "Components",
            "SettingsPage.cs"));

        Assert.Contains(".WithKey($\"settings-theme-{settings.LanguageTag}\")", settingsPage);
        Assert.Contains(".WithKey($\"settings-language-{settings.LanguageTag}\")", settingsPage);
        Assert.Contains(".WithKey($\"settings-refresh-{settings.LanguageTag}\")", settingsPage);
        Assert.Contains(".WithKey($\"settings-terminal-{settings.LanguageTag}\")", settingsPage);
    }

    [Fact]
    public void ResourceKeysAreReferencedByApplicationCode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src", "Wessl.App"),
                "*.cs",
                SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var referencedKeys = Regex.Matches(
                appSource,
                "(?:(?:AppStrings\\.)?(?:Get|Format)|new(?:\\s+LocalizedMessage)?)\\s*\\(\\s*\"(?<key>[A-Za-z][A-Za-z0-9]+)\"")
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var action in new[] { "Start", "Stop", "Restart", "Remove" })
        {
            foreach (var state in new[] { "Pending", "Succeeded", "Failed" })
            {
                referencedKeys.Add($"Action{action}{state}");
            }
        }
        referencedKeys.UnionWith([
            "ImagesPullPending",
            "ImagesPullSuccess",
            "ImagesPullError",
            "ImagesTagPending",
            "ImagesTagSuccess",
            "ImagesTagError",
            "ImagesPruneError",
            "ImagesPruneSuccess",
            "ImagesPrunePending",
            "PortOpenSuccess",
            "PortOpenFailure",
            "LogsFollowing",
            "LogsFollowingTruncated",
            "LogsEnded",
            "LogsEndedTruncated",
            "ContainerStateUnknown",
            "ContainerStateRunning",
            "ContainerStateStopped"
        ]);

        var resourceKeys = LoadResources()["en-US"].Keys.ToHashSet(StringComparer.Ordinal);
        Assert.Empty(resourceKeys.Except(referencedKeys));
        Assert.Empty(referencedKeys.Except(resourceKeys));
    }

    private static Dictionary<string, Dictionary<string, string>> LoadResources() =>
        Directory.EnumerateFiles(
                Path.Combine(AppContext.BaseDirectory, "Strings"),
                "Resources.resw",
                SearchOption.AllDirectories)
            .ToDictionary(
                path => new DirectoryInfo(Path.GetDirectoryName(path)!).Name,
                LoadLanguage,
                StringComparer.Ordinal);

    private static Dictionary<string, string> LoadLanguage(string path)
    {
        var entries = XDocument.Load(path).Root!.Elements("data").ToArray();
        var keys = entries.Select(entry => (string)entry.Attribute("name")!).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());

        return entries.ToDictionary(
            entry => (string)entry.Attribute("name")!,
            entry => (string?)entry.Element("value") ?? string.Empty,
            StringComparer.Ordinal);
    }

    private static string[] GetPlaceholders(string value) =>
        PlaceholderPattern.Matches(value)
            .Select(match => match.Groups[1].Value)
            .OrderBy(index => index, StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wessl.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Wessl.slnx from the test output directory.");
    }
}
