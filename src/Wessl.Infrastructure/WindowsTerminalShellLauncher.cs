using System.Diagnostics;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class WindowsTerminalShellLauncher : IContainerShellLauncher
{
    public void OpenShell(string containerId, ContainerTerminal terminal)
    {
        var startInfo = CreateStartInfo(containerId, terminal);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {terminal}.");
    }

    internal static ProcessStartInfo CreateStartInfo(
        string containerId,
        ContainerTerminal terminal = ContainerTerminal.WindowsTerminal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        if (!char.IsAsciiLetterOrDigit(containerId[0])
            || containerId.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '.' and not '-'))
        {
            throw new ArgumentException("The container ID contains unsupported characters.", nameof(containerId));
        }

        var command = new[] { "wslc", "exec", "-it", containerId, "/bin/sh" };
        var startInfo = terminal switch
        {
            ContainerTerminal.WindowsTerminal => CreateStartInfo("wt.exe", ["new-tab", .. command]),
            ContainerTerminal.PowerShell => CreateStartInfo(
                "powershell.exe",
                ["-NoExit", "-Command", string.Join(" ", command)]),
            ContainerTerminal.CommandPrompt => CreateStartInfo(
                "cmd.exe",
                ["/K", string.Join(" ", command)]),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal))
        };

        return startInfo;
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
