using SemaNami.Cli;
using Xunit;

namespace SemaNami.Tests;

public class CliArgsTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsShowUsage()
    {
        var result = CliArgs.Parse(Array.Empty<string>());

        Assert.IsType<CliCommand.ShowUsage>(result);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Parse_HelpFlag_ReturnsShowUsage(string flag)
    {
        var result = CliArgs.Parse(new[] { flag });

        Assert.IsType<CliCommand.ShowUsage>(result);
    }

    [Fact]
    public void Parse_GetChatIdFlag_ReturnsGetChatId()
    {
        var result = CliArgs.Parse(new[] { "--get-chat-id" });

        Assert.IsType<CliCommand.GetChatId>(result);
    }

    [Fact]
    public void Parse_SetupFlag_ReturnsRunSetup()
    {
        var result = CliArgs.Parse(new[] { "--setup" });

        Assert.IsType<CliCommand.RunSetup>(result);
    }

    [Fact]
    public void Parse_SenderAndMessage_ReturnsSendWithBoth()
    {
        var result = CliArgs.Parse(new[] { "-sender", "Build Script", "-message", "Build finished" });

        var send = Assert.IsType<CliCommand.Send>(result);
        Assert.Equal("Build Script", send.Sender);
        Assert.Equal("Build finished", send.Message);
    }

    [Fact]
    public void Parse_MessageBeforeSender_OrderDoesNotMatter()
    {
        var result = CliArgs.Parse(new[] { "-message", "Build finished", "-sender", "Build Script" });

        var send = Assert.IsType<CliCommand.Send>(result);
        Assert.Equal("Build Script", send.Sender);
        Assert.Equal("Build finished", send.Message);
    }

    [Fact]
    public void Parse_MissingSender_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "-message", "Build finished" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_MissingMessage_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "-sender", "Build Script" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankSender_ReturnsInvalid(string blankSender)
    {
        var result = CliArgs.Parse(new[] { "-sender", blankSender, "-message", "Build finished" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankMessage_ReturnsInvalid(string blankMessage)
    {
        var result = CliArgs.Parse(new[] { "-sender", "Build Script", "-message", blankMessage });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_UnrecognizedFlag_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "-sender", "Build Script", "-message", "hi", "--bogus" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_FlagMissingItsValue_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "-sender" });

        Assert.IsType<CliCommand.Invalid>(result);
    }
}
