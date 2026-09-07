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

    [Fact]
    public void Parse_ConversationFlag_FoldsIntoSend()
    {
        var result = CliArgs.Parse(new[] { "-sender", "Build Script", "-message", "hi", "-conversation", "deploy-1" });

        var send = Assert.IsType<CliCommand.Send>(result);
        Assert.Equal("deploy-1", send.ConversationId);
    }

    [Fact]
    public void Parse_SendWithoutConversation_ConversationIdIsNull()
    {
        var result = CliArgs.Parse(new[] { "-sender", "Build Script", "-message", "hi" });

        var send = Assert.IsType<CliCommand.Send>(result);
        Assert.Null(send.ConversationId);
    }

    [Fact]
    public void Parse_ConversationHistory_WithSenderAndAfter_ParsesAll()
    {
        var result = CliArgs.Parse(new[] { "--conversation-history", "deploy-1", "-sender", "Build Script", "-after", "42" });

        var history = Assert.IsType<CliCommand.ConversationHistory>(result);
        Assert.Equal("deploy-1", history.ConversationId);
        Assert.Equal("Build Script", history.Sender);
        Assert.Equal(42, history.After);
    }

    [Fact]
    public void Parse_ConversationHistory_WithoutAfter_AfterIsNull()
    {
        var result = CliArgs.Parse(new[] { "--conversation-history", "deploy-1", "-sender", "Build Script" });

        var history = Assert.IsType<CliCommand.ConversationHistory>(result);
        Assert.Null(history.After);
    }

    [Fact]
    public void Parse_ConversationHistory_NonNumericAfter_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "--conversation-history", "deploy-1", "-sender", "Build Script", "-after", "notanumber" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_ConversationHistory_MissingSender_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "--conversation-history", "deploy-1" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_CloseConversation_WithSender_Parses()
    {
        var result = CliArgs.Parse(new[] { "--close-conversation", "deploy-1", "-sender", "Build Script" });

        var close = Assert.IsType<CliCommand.CloseConversation>(result);
        Assert.Equal("deploy-1", close.ConversationId);
        Assert.Equal("Build Script", close.Sender);
    }

    [Fact]
    public void Parse_CloseConversation_MissingSender_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "--close-conversation", "deploy-1" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_CloseConversation_MissingConversationId_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "--close-conversation" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Theory]
    [InlineData("--listen")]
    [InlineData("--install-service")]
    [InlineData("--uninstall-service")]
    public void Parse_SingleFlagCommands_ParseToTheirOwnCommand(string flag)
    {
        var result = CliArgs.Parse(new[] { flag });

        Assert.Equal(flag switch
        {
            "--listen" => typeof(CliCommand.Listen),
            "--install-service" => typeof(CliCommand.InstallService),
            "--uninstall-service" => typeof(CliCommand.UninstallService),
            _ => throw new InvalidOperationException(),
        }, result.GetType());
    }

    [Fact]
    public void Parse_WaitForReply_WithAllArguments_ParsesAll()
    {
        var result = CliArgs.Parse(new[]
        {
            "--wait-for-reply", "-sender", "Build Script", "-conversation", "deploy-1", "-after", "5", "--timeout", "60",
        });

        var wait = Assert.IsType<CliCommand.WaitForReply>(result);
        Assert.Equal("Build Script", wait.Sender);
        Assert.Equal("deploy-1", wait.ConversationId);
        Assert.Equal(5, wait.After);
        Assert.Equal(60, wait.TimeoutSeconds);
    }

    [Fact]
    public void Parse_WaitForReply_WithoutTimeout_UsesDefault()
    {
        var result = CliArgs.Parse(new[] { "--wait-for-reply", "-sender", "Build Script", "-conversation", "deploy-1" });

        var wait = Assert.IsType<CliCommand.WaitForReply>(result);
        Assert.True(wait.TimeoutSeconds > 0);
    }

    [Fact]
    public void Parse_WaitForReply_MissingConversation_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[] { "--wait-for-reply", "-sender", "Build Script" });

        Assert.IsType<CliCommand.Invalid>(result);
    }

    [Fact]
    public void Parse_WaitForReply_NonNumericTimeout_ReturnsInvalid()
    {
        var result = CliArgs.Parse(new[]
        {
            "--wait-for-reply", "-sender", "Build Script", "-conversation", "deploy-1", "--timeout", "soon",
        });

        Assert.IsType<CliCommand.Invalid>(result);
    }
}
