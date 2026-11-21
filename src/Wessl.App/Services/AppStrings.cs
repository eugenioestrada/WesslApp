using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Wessl.Domain;
using Windows.Globalization;

namespace Wessl.App.Services;

public static class AppStrings
{
    private static ResourceMap _resources = CreateResources();

    private static ResourceMap CreateResources()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable path is not available.");
        var priPath = Path.ChangeExtension(executablePath, ".pri");

        return new ResourceManager(priPath).MainResourceMap.GetSubtree("Resources");
    }

    public static string Get(string name) =>
        _resources.GetValue(name).ValueAsString;

    public static void SetLanguage(string languageTag)
    {
        ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        var culture = CultureInfo.GetCultureInfo(languageTag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        _resources = CreateResources();
    }

    public static string Format(string name, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(name), args);

    public static string Format(LocalizedMessage message) =>
        Format(
            message.ResourceKey,
            message.Arguments.Select(argument =>
                argument is LocalizedMessage nested ? Format(nested) : argument).ToArray());

    public static string GetContainerState(ContainerState state) =>
        Get(state switch
        {
            ContainerState.Running => "ContainerStateRunning",
            ContainerState.Stopped => "ContainerStateStopped",
            _ => "ContainerStateUnknown"
        });
}
