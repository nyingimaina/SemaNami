using SemaNami.Cli;
using Xunit;

namespace SemaNami.Tests;

public class ConsoleWindowHiderTests
{
    // The actual GetConsoleWindow/ShowWindow calls are Win32 interop with no meaningful behavior
    // under a test runner (no dedicated console to hide), so only the decision logic — whether
    // this process is the console's sole owner — is unit tested here.

    [Fact]
    public void ShouldHide_SoleOwnerOfConsole_ReturnsTrue()
    {
        // Exactly one process attached to the console means Windows created it fresh for us
        // (e.g. an ONLOGON scheduled task launching --listen with no parent shell) — safe to hide.
        Assert.True(ConsoleWindowHider.ShouldHide(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void ShouldHide_NotSoleOwner_ReturnsFalse(int consoleProcessCount)
    {
        // 0 means no console at all (nothing to hide); 2+ means the console is shared with an
        // interactive parent shell the user is actively using — must never hide that.
        Assert.False(ConsoleWindowHider.ShouldHide(consoleProcessCount));
    }
}
