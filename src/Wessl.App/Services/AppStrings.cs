using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Wessl.Domain;

namespace Wessl.App.Services;

public static class AppStrings
{
    private static readonly ResourceMap Resources = CreateResources();

    private static ResourceMap CreateResources()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable path is not available.");
        var priPath = Path.ChangeExtension(executablePath, ".pri");

        return new ResourceManager(priPath).MainResourceMap.GetSubtree("Resources");
    }

    public static string Get(string name) =>
        Resources.GetValue(name).ValueAsString;

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
