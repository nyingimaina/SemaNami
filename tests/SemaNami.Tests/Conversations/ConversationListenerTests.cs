using Moq;
using SemaNami.Core.Conversations;
using Xunit;

namespace SemaNami.Tests.Conversations;

public class ConversationListenerTests : IDisposable
{
    private readonly Mock<IUpdatesSource> updatesSource = new();
    private readonly Mock<IRealtimeNotifier> realtimeNotifier = new();
    private readonly List<string> tempDbPaths = new();

    public void Dispose()
    {
        foreach (var path in tempDbPaths)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private SqliteConversationStore CreateStore()
    {
        var path = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".db");
        tempDbPaths.Add(path);
        return new SqliteConversationStore(path);
    }

    private void SetupUpdates(params IncomingUpdate[] updates) =>
        updatesSource.Setup(u => u.GetUpdatesAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updates);

    private ConversationListener CreateListener(IConversationStore store) =>
        new(store, updatesSource.Object, realtimeNotifier.Object);

    [Fact]
    public async Task PollOnceAsync_ReplyToAnOlderMessage_StillMatchesTheConversation()
    {
        var store = CreateStore();
        store.RecordSentMessage("Build Script", "deploy-1", 100, "First question");
        store.RecordSentMessage("Build Script", "deploy-1", 101, "Second question");
        // Reply targets the OLDER message (100), not the latest (101) — regression test.
        SetupUpdates(new IncomingUpdate(1, 999, 500, ReplyToMessageId: 100, "Answering the first one"));

        await CreateListener(store).PollOnceAsync();

        var history = store.GetHistory("Build Script", "deploy-1", null);
        Assert.Contains(history, m => m.Text == "Answering the first one");
    }

    [Fact]
    public async Task PollOnceAsync_NonReplyMessage_ZeroOpenConversations_RoutesToUnmatched()
    {
        var store = CreateStore();
        SetupUpdates(new IncomingUpdate(1, 999, 500, ReplyToMessageId: null, "hello?"));

        await CreateListener(store).PollOnceAsync();

        var history = store.GetHistory("_system", "_unmatched", null);
        var message = Assert.Single(history);
        Assert.False(message.AmbiguousMatch);
    }

    [Fact]
    public async Task PollOnceAsync_NonReplyMessage_ExactlyOneOpenConversation_AutoAttributesUnambiguously()
    {
        var store = CreateStore();
        store.RecordSentMessage("Build Script", "deploy-1", 100, "Ready?");
        SetupUpdates(new IncomingUpdate(1, 999, 500, ReplyToMessageId: null, "Yes"));

        await CreateListener(store).PollOnceAsync();

        var history = store.GetHistory("Build Script", "deploy-1", null);
        Assert.Contains(history, m => m.Text == "Yes" && !m.AmbiguousMatch);
    }

    [Fact]
    public async Task PollOnceAsync_NonReplyMessage_MultipleOpenConversations_AttributesToMostRecentAndFlagsAmbiguous()
    {
        var store = CreateStore();
        store.RecordSentMessage("Sender A", "old", 100, "Older question");
        await Task.Delay(10);
        store.RecordSentMessage("Sender B", "new", 200, "Newer question");
        SetupUpdates(new IncomingUpdate(1, 999, 500, ReplyToMessageId: null, "reply"));

        await CreateListener(store).PollOnceAsync();

        var newHistory = store.GetHistory("Sender B", "new", null);
        Assert.Contains(newHistory, m => m.Text == "reply" && m.AmbiguousMatch);
        var oldHistory = store.GetHistory("Sender A", "old", null);
        Assert.DoesNotContain(oldHistory, m => m.Text == "reply");
    }

    [Fact]
    public async Task PollOnceAsync_AdvancesAndPersistsTheOffset()
    {
        var store = CreateStore();
        SetupUpdates(new IncomingUpdate(41, 999, 500, null, "hi"));

        await CreateListener(store).PollOnceAsync();

        Assert.Equal(42, store.GetLastUpdateId());
    }

    [Fact]
    public async Task PollOnceAsync_ReplayingTheSameUpdateAfterASimulatedRestart_DoesNotDuplicate()
    {
        var store = CreateStore();
        SetupUpdates(new IncomingUpdate(41, 999, 500, null, "hi"));

        await CreateListener(store).PollOnceAsync();
        // Simulate a restart before the offset advanced far enough to exclude this update from
        // being handed back again by Telegram.
        await CreateListener(store).PollOnceAsync();

        var history = store.GetHistory("_system", "_unmatched", null);
        Assert.Single(history);
    }

    [Fact]
    public async Task PollOnceAsync_ReturnsCountOfProcessedUpdates()
    {
        var store = CreateStore();
        SetupUpdates(
            new IncomingUpdate(1, 999, 500, null, "a"),
            new IncomingUpdate(2, 999, 501, null, "b"));

        var count = await CreateListener(store).PollOnceAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PollOnceAsync_NotifiesRealtimeNotifier_OnlyAfterTheMessageIsPersisted()
    {
        var store = CreateStore();
        store.RecordSentMessage("Build Script", "deploy-1", 100, "Ready?");
        SetupUpdates(new IncomingUpdate(1, 999, 500, ReplyToMessageId: 100, "Yes"));

        var wasPersistedAtNotifyTime = false;
        realtimeNotifier
            .Setup(n => n.PublishNewMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StoredMessage>()))
            .Callback<string, string, StoredMessage>((sender, conversationId, message) =>
            {
                var history = store.GetHistory(sender, conversationId, null);
                wasPersistedAtNotifyTime = history.Any(m => m.TelegramMessageId == message.TelegramMessageId);
            });

        await CreateListener(store).PollOnceAsync();

        realtimeNotifier.Verify(n => n.PublishNewMessage("Build Script", "deploy-1", It.IsAny<StoredMessage>()), Times.Once);
        Assert.True(wasPersistedAtNotifyTime);
    }
}
