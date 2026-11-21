using Wessl.Infrastructure;
using Wessl.Domain;

namespace Wessl.Tests;

public sealed class WindowsTerminalShellLauncherTests
{
    [Fact]
    public void CreateStartInfoUsesArgumentListForInteractiveWslcExec()
    {
        var startInfo = WindowsTerminalShellLauncher.CreateStartInfo("container-123");

        Assert.Equal("wt.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(
            ["new-tab", "wslc", "exec", "-it", "container-123", "/bin/sh"],
            startInfo.ArgumentList);
    }

    [Theory]
    [InlineData(ContainerTerminal.WindowsTerminal, "wt.exe", "new-tab")]
    [InlineData(ContainerTerminal.PowerShell, "powershell.exe", "-NoExit")]
    [InlineData(ContainerTerminal.CommandPrompt, "cmd.exe", "/K")]
    public void CreateStartInfoUsesTheSelectedTerminal(
        ContainerTerminal terminal,
        string fileName,
        string firstArgument)
    {
        var startInfo = WindowsTerminalShellLauncher.CreateStartInfo("container-123", terminal);

        Assert.Equal(fileName, startInfo.FileName);
        Assert.Equal(firstArgument, startInfo.ArgumentList[0]);
        var arguments = string.Join(" ", startInfo.ArgumentList);
        Assert.Contains("wslc", arguments);
        Assert.Contains("container-123", arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--help")]
    [InlineData("container; whoami")]
    public void CreateStartInfoRejectsInvalidContainerIds(string containerId)
    {
        Assert.Throws<ArgumentException>(() => WindowsTerminalShellLauncher.CreateStartInfo(containerId));
    }

    [Fact]
    public void CreateStartInfoRejectsUnsupportedTerminal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WindowsTerminalShellLauncher.CreateStartInfo("container-123", (ContainerTerminal)99));
    }
}
