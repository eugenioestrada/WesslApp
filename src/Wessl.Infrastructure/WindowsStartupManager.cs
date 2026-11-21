using Microsoft.Win32;
using System.Runtime.Versioning;
using Wessl.Domain;

namespace Wessl.Infrastructure;

public sealed class WindowsStartupManager : IStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wessl";

    [SupportedOSPlatform("windows")]
    public void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("The application executable path is not available.");
            runKey.SetValue(ValueName, QuoteExecutablePath(executablePath), RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    internal static string QuoteExecutablePath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\"";
    }
}
