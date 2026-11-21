using System.Diagnostics;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class WindowsTerminalShellLauncher : IContainerShellLauncher
{
    public void OpenShell(string containerId)
    {
        var startInfo = CreateStartInfo(containerId);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Windows Terminal.");
    }

    internal static ProcessStartInfo CreateStartInfo(string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        if (!char.IsAsciiLetterOrDigit(containerId[0])
            || containerId.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '.' and not '-'))
        {
            throw new ArgumentException("The container ID contains unsupported characters.", nameof(containerId));
        }

        var startInfo = new ProcessStartInfo("wt.exe")
        {
            UseShellExecute = false
        };
        foreach (var argument in new[] { "new-tab", "wslc", "exec", "-it", containerId, "/bin/sh" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
