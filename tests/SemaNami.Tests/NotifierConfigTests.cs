using SemaNami.Core;
using Xunit;

namespace SemaNami.Tests;

public class NotifierConfigTests
{
    [Fact]
    public void FromEnvironment_BothVariablesPresent_ReturnsConfig()
    {
        var config = NotifierConfig.FromEnvironment(name => name switch
        {
            "TELEGRAM_BOT_TOKEN" => "123:abc",
            "TELEGRAM_CHAT_ID" => "999",
            _ => null,
        });

        Assert.NotNull(config);
        Assert.Equal("123:abc", config!.BotToken);
        Assert.Equal("999", config.ChatId);
    }

    [Fact]
    public void FromEnvironment_BotTokenMissing_ReturnsNull()
    {
        var config = NotifierConfig.FromEnvironment(name => name == "TELEGRAM_CHAT_ID" ? "999" : null);

        Assert.Null(config);
    }

    [Fact]
    public void FromEnvironment_ChatIdMissing_ReturnsNull()
    {
        var config = NotifierConfig.FromEnvironment(name => name == "TELEGRAM_BOT_TOKEN" ? "123:abc" : null);

        Assert.Null(config);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromEnvironment_BlankBotToken_ReturnsNull(string blankToken)
    {
        var config = NotifierConfig.FromEnvironment(name => name switch
        {
            "TELEGRAM_BOT_TOKEN" => blankToken,
            "TELEGRAM_CHAT_ID" => "999",
            _ => null,
        });

        Assert.Null(config);
    }
}
