using Moq;
using SemaNami.Core;
using SemaNami.Core.Conversations;
using Xunit;

namespace SemaNami.Tests.Conversations;

public class ConversationSenderTests : IDisposable
{
    private readonly Mock<ITelegramMessageSender> messageSender = new();
    private readonly List<string> tempDbPaths = new();

    public void Dispose()
    {
        foreach (var path in tempDbPaths)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private ConversationSender CreateSender(IConversationStore store) => new(store, messageSender.Object, "999");

    private SqliteConversationStore CreateStore()
    {
        var path = TempDbPath();
        tempDbPaths.Add(path);
        return new SqliteConversationStore(path);
    }

    [Fact]
    public async Task SendAsync_NewConversation_SendsWithNoReplyTarget()
    {
        var store = CreateStore();
        messageSender.Setup(s => s.SendReplyAsync("999", "hello", null, It.IsAny<CancellationToken>())).ReturnsAsync(100);

        await CreateSender(store).SendAsync("Build Script", "deploy-1", "hello");

        messageSender.Verify(s => s.SendReplyAsync("999", "hello", null, It.IsAny<CancellationToken>()), Times.Once);
        var conversation = store.GetConversation("Build Script", "deploy-1");
        Assert.Equal(100, conversation!.LastMessageId);
    }

    [Fact]
    public async Task SendAsync_ExistingConversation_RepliesToStoredLastMessageId()
    {
        var store = CreateStore();
        store.RecordReceivedMessage("Build Script", "deploy-1", 200, "earlier reply", false);
        messageSender.Setup(s => s.SendReplyAsync("999", "follow up", 200, It.IsAny<CancellationToken>())).ReturnsAsync(201);

        await CreateSender(store).SendAsync("Build Script", "deploy-1", "follow up");

        messageSender.Verify(s => s.SendReplyAsync("999", "follow up", 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_TwoSendersSameConversationId_ProduceIndependentConversations()
    {
        var store = CreateStore();
        messageSender.SetupSequence(s => s.SendReplyAsync("999", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100)
            .ReturnsAsync(200);

        var sender = CreateSender(store);
        await sender.SendAsync("Sender A", "build", "from A");
        await sender.SendAsync("Sender B", "build", "from B");

        Assert.Single(store.GetHistory("Sender A", "build", null));
        Assert.Single(store.GetHistory("Sender B", "build", null));
    }

    private static string TempDbPath() => Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".db");
}
