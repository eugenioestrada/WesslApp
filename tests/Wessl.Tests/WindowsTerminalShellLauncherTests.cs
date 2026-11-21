using Wessl.Infrastructure;

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
    [InlineData("")]
    [InlineData("--help")]
    [InlineData("container; whoami")]
    public void CreateStartInfoRejectsInvalidContainerIds(string containerId)
    {
        Assert.Throws<ArgumentException>(() => WindowsTerminalShellLauncher.CreateStartInfo(containerId));
    }
}
