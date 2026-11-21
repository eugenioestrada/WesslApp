using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Wessl.Domain;

namespace Wessl.App.Services;

public static class AppStrings
{
    private static readonly ResourceManager _manager = CreateResourceManager();
    private static readonly ResourceMap _resources = _manager.MainResourceMap.GetSubtree("Resources");
    private static readonly ResourceContext _context = _manager.CreateResourceContext();

    private static ResourceManager CreateResourceManager()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable path is not available.");
        var priPath = Path.ChangeExtension(executablePath, ".pri");

        return new ResourceManager(priPath);
    }

    public static string Get(string name) =>
        _resources.GetValue(name, _context).ValueAsString;

    public static void SetLanguage(string languageTag)
    {
        var culture = CultureInfo.GetCultureInfo(languageTag);
        _context.QualifierValues["Language"] = languageTag;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
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
