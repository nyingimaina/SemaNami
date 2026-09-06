using TelegramNotifier.Cli;
using Xunit;

namespace TelegramNotifier.Tests;

public class CliArgsTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsInvalid()
    {
        var result = CliArgs.Parse(Array.Empty<string>());

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_GetChatIdFlag_ReturnsGetChatId()
    {
        var result = CliArgs.Parse(new[] { "--get-chat-id" });

        Assert.IsType<CliCommand.GetChatId>(result);
    }

    [Fact]
    public void Parse_SingleMessageArg_ReturnsSendWithThatMessage()
    {
        var result = CliArgs.Parse(new[] { "build finished" });

        var send = Assert.IsType<CliCommand.Send>(result);
        Assert.Equal("build finished", send.Message);
    }

    [Fact]
    public void Parse_MultipleUnquotedWords_JoinsThemIntoOneMessage()
    {
        var result = CliArgs.Parse(new[] { "build", "finished", "successfully" });

        var send = Assert.IsType<CliCommand.Send>(result);
        Assert.Equal("build finished successfully", send.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankMessage_ReturnsInvalid(string blankMessage)
    {
        var result = CliArgs.Parse(new[] { blankMessage });

        Assert.IsType<CliCommand.Invalid>(result);
    }
}
