using Moq;
using SemaNami.Core;
using Xunit;

namespace SemaNami.Tests;

public class SetupWizardTests
{
    private readonly Mock<ISetupIO> io = new();
    private readonly Mock<IEnvironmentPersister> persister = new();
    private readonly Mock<IBotValidator> validator = new();
    private readonly Mock<IChatIdLookup> chatIdLookup = new();
    private readonly Mock<ITelegramMessageSender> sender = new();

    private SetupWizard CreateWizard() => new(
        io.Object,
        persister.Object,
        _ => validator.Object,
        _ => chatIdLookup.Object,
        _ => sender.Object);

    [Fact]
    public async Task RunAsync_AlreadyConfigured_DoesNothingAndReturnsTrue()
    {
        persister.Setup(p => p.ReadExisting()).Returns(("123:abc", "999"));

        var result = await CreateWizard().RunAsync();

        Assert.True(result);
        validator.Verify(v => v.TryGetBotUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        persister.Verify(p => p.Persist(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_BlankTokenEntered_ReturnsFalseWithoutValidating()
    {
        persister.Setup(p => p.ReadExisting()).Returns((null, null));
        io.Setup(i => i.ReadLine()).Returns("   ");

        var result = await CreateWizard().RunAsync();

        Assert.False(result);
        validator.Verify(v => v.TryGetBotUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_InvalidToken_ReturnsFalseWithoutLookingUpChatId()
    {
        persister.Setup(p => p.ReadExisting()).Returns((null, null));
        io.Setup(i => i.ReadLine()).Returns("bad-token");
        validator.Setup(v => v.TryGetBotUsernameAsync("bad-token", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var result = await CreateWizard().RunAsync();

        Assert.False(result);
        chatIdLookup.Verify(c => c.GetRecentChatIdsAsync(It.IsAny<CancellationToken>()), Times.Never);
        persister.Verify(p => p.Persist(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ValidTokenAndChatIdFoundImmediately_PersistsAndSendsConfirmation()
    {
        persister.Setup(p => p.ReadExisting()).Returns((null, null));
        io.SetupSequence(i => i.ReadLine())
            .Returns("123:abc")   // token
            .Returns("");         // "press enter once you've messaged the bot"
        validator.Setup(v => v.TryGetBotUsernameAsync("123:abc", It.IsAny<CancellationToken>())).ReturnsAsync("my_bot");
        chatIdLookup.Setup(c => c.GetRecentChatIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new long[] { 555 });

        var result = await CreateWizard().RunAsync();

        Assert.True(result);
        persister.Verify(p => p.Persist("123:abc", "555"), Times.Once);
        sender.Verify(s => s.SendMessageAsync("555", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ChatIdFoundOnRetry_LoopsThenSucceeds()
    {
        persister.Setup(p => p.ReadExisting()).Returns((null, null));
        io.SetupSequence(i => i.ReadLine())
            .Returns("123:abc")   // token
            .Returns("")          // "press enter once you've messaged the bot"
            .Returns("");         // "press enter to check again" (after first empty lookup)
        validator.Setup(v => v.TryGetBotUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("my_bot");
        chatIdLookup.SetupSequence(c => c.GetRecentChatIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>())
            .ReturnsAsync(new long[] { 555 });

        var result = await CreateWizard().RunAsync();

        Assert.True(result);
        chatIdLookup.Verify(c => c.GetRecentChatIdsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        persister.Verify(p => p.Persist(It.IsAny<string>(), "555"), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ChatIdNeverFound_ReturnsFalseWithoutPersisting()
    {
        persister.Setup(p => p.ReadExisting()).Returns((null, null));
        io.SetupSequence(i => i.ReadLine())
            .Returns("123:abc")
            .Returns("")
            .Returns("");
        validator.Setup(v => v.TryGetBotUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("my_bot");
        chatIdLookup.Setup(c => c.GetRecentChatIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<long>());

        var result = await CreateWizard().RunAsync();

        Assert.False(result);
        persister.Verify(p => p.Persist(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        sender.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
