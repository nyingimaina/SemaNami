using Moq;
using SemaNami.Core;
using Xunit;

namespace SemaNami.Tests;

public class NotifierTests
{
    [Fact]
    public async Task NotifyAsync_ValidMessage_SendsThroughTheConfiguredChatId()
    {
        var senderMock = new Mock<ITelegramMessageSender>();
        var notifier = new Notifier(senderMock.Object, "999");

        await notifier.NotifyAsync("build finished");

        senderMock.Verify(s => s.SendMessageAsync("999", "build finished", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task NotifyAsync_BlankMessage_ThrowsWithoutCallingSender(string? message)
    {
        var senderMock = new Mock<ITelegramMessageSender>();
        var notifier = new Notifier(senderMock.Object, "999");

        await Assert.ThrowsAsync<ArgumentException>(() => notifier.NotifyAsync(message!));

        senderMock.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_BlankChatId_Throws(string? chatId)
    {
        var senderMock = new Mock<ITelegramMessageSender>();

        Assert.Throws<ArgumentException>(() => new Notifier(senderMock.Object, chatId!));
    }

    [Fact]
    public void Constructor_NullSender_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Notifier(null!, "999"));
    }
}
