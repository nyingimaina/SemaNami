using SemaNami.Core;
using Xunit;

namespace SemaNami.Tests;

public class MessageFormatterTests
{
    [Fact]
    public void WithSender_PrefixesMessageWithSenderName()
    {
        var result = MessageFormatter.WithSender("Build Script", "Build finished");

        Assert.Equal("Build Script: Build finished", result);
    }
}
