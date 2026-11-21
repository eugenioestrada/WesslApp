using Wessl.Infrastructure;

namespace Wessl.Tests;

public sealed class WindowsStartupManagerTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Wessl\wessl.exe", "\"C:\\Program Files\\Wessl\\wessl.exe\"")]
    [InlineData(@"C:\Wessl\wessl.exe", "\"C:\\Wessl\\wessl.exe\"")]
    public void QuoteExecutablePathCreatesARegistryRunCommand(string executablePath, string expected)
    {
        Assert.Equal(expected, WindowsStartupManager.QuoteExecutablePath(executablePath));
    }

    [Fact]
    public void QuoteExecutablePathRejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => WindowsStartupManager.QuoteExecutablePath(""));
    }
}
