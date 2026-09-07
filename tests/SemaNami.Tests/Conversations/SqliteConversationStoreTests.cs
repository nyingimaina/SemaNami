using SemaNami.Core.Conversations;
using Xunit;

namespace SemaNami.Tests.Conversations;

public class SqliteConversationStoreTests : IDisposable
{
    private readonly string dbPath;

    public SqliteConversationStoreTests()
    {
        dbPath = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".db");
    }

    public void Dispose()
    {
        if (File.Exists(dbPath)) File.Delete(dbPath);
        var wal = dbPath + "-wal";
        var shm = dbPath + "-shm";
        if (File.Exists(wal)) File.Delete(wal);
        if (File.Exists(shm)) File.Delete(shm);
    }

    [Fact]
    public void Constructing_Twice_AgainstSameFile_IsIdempotent()
    {
        _ = new SqliteConversationStore(dbPath);
        var exception = Record.Exception(() => new SqliteConversationStore(dbPath));

        Assert.Null(exception);
    }

    [Fact]
    public void GetConversation_NoneRecorded_ReturnsNull()
    {
        var store = new SqliteConversationStore(dbPath);

        Assert.Null(store.GetConversation("Build Script", "deploy-1"));
    }

    [Fact]
    public void RecordSentMessage_CreatesConversationAndMessage()
    {
        var store = new SqliteConversationStore(dbPath);

        store.RecordSentMessage("Build Script", "deploy-1", 100, "Deploy ready?");

        var conversation = store.GetConversation("Build Script", "deploy-1");
        Assert.NotNull(conversation);
        Assert.Equal("open", conversation!.Status);
        Assert.Equal(100, conversation.LastMessageId);

        var history = store.GetHistory("Build Script", "deploy-1", null);
        var message = Assert.Single(history);
        Assert.Equal("sent", message.Direction);
        Assert.Equal(100, message.TelegramMessageId);
        Assert.Equal("Deploy ready?", message.Text);
    }

    [Fact]
    public void RecordReceivedMessage_AppendsAndAdvancesLastMessageId()
    {
        var store = new SqliteConversationStore(dbPath);
        store.RecordSentMessage("Build Script", "deploy-1", 100, "Deploy ready?");

        store.RecordReceivedMessage("Build Script", "deploy-1", 101, "Yes", ambiguousMatch: false);

        var conversation = store.GetConversation("Build Script", "deploy-1");
        Assert.Equal(101, conversation!.LastMessageId);

        var history = store.GetHistory("Build Script", "deploy-1", null);
        Assert.Equal(2, history.Count);
        Assert.Equal("received", history[1].Direction);
        Assert.False(history[1].AmbiguousMatch);
    }

    [Fact]
    public void TwoSenders_SameConversationId_AreCompletelyIndependent()
    {
        var store = new SqliteConversationStore(dbPath);

        store.RecordSentMessage("Sender A", "build", 100, "From A");
        store.RecordSentMessage("Sender B", "build", 200, "From B");

        var historyA = store.GetHistory("Sender A", "build", null);
        var historyB = store.GetHistory("Sender B", "build", null);

        Assert.Equal("From A", Assert.Single(historyA).Text);
        Assert.Equal("From B", Assert.Single(historyB).Text);
    }

    [Fact]
    public void GetHistory_RespectsAfterSeqCutoff()
    {
        var store = new SqliteConversationStore(dbPath);
        store.RecordSentMessage("Build Script", "deploy-1", 100, "First");
        var firstSeq = store.GetHistory("Build Script", "deploy-1", null)[0].Seq;
        store.RecordReceivedMessage("Build Script", "deploy-1", 101, "Second", false);

        var afterFirst = store.GetHistory("Build Script", "deploy-1", firstSeq);

        var onlyMessage = Assert.Single(afterFirst);
        Assert.Equal("Second", onlyMessage.Text);
    }

    [Fact]
    public void GetHistory_UnknownConversation_ReturnsEmpty()
    {
        var store = new SqliteConversationStore(dbPath);

        Assert.Empty(store.GetHistory("Nobody", "nothing", null));
    }

    [Fact]
    public void TryFindConversationByMessageId_MatchesAnyRecordedMessage_NotJustTheLatest()
    {
        var store = new SqliteConversationStore(dbPath);
        store.RecordSentMessage("Build Script", "deploy-1", 100, "First question");
        store.RecordSentMessage("Build Script", "deploy-1", 101, "Second question");

        // Regression test: replying to the OLDER message (100) must still resolve, not just 101.
        var found = store.TryFindConversationByMessageId(100);

        Assert.NotNull(found);
        Assert.Equal("Build Script", found!.Value.Sender);
        Assert.Equal("deploy-1", found.Value.ConversationId);
    }

    [Fact]
    public void TryFindConversationByMessageId_Unknown_ReturnsNull()
    {
        var store = new SqliteConversationStore(dbPath);

        Assert.Null(store.TryFindConversationByMessageId(999));
    }

    [Fact]
    public void GetOpenConversations_ReturnsAcrossAllSenders()
    {
        var store = new SqliteConversationStore(dbPath);
        store.RecordSentMessage("Sender A", "a1", 100, "hi");
        store.RecordSentMessage("Sender B", "b1", 200, "hi");

        var open = store.GetOpenConversations();

        Assert.Equal(2, open.Count);
    }

    [Fact]
    public void CloseConversation_RemovesItFromOpenSet_WithoutAffectingOtherSenders()
    {
        var store = new SqliteConversationStore(dbPath);
        store.RecordSentMessage("Sender A", "same-id", 100, "hi");
        store.RecordSentMessage("Sender B", "same-id", 200, "hi");

        store.CloseConversation("Sender A", "same-id");

        var open = store.GetOpenConversations();
        var remaining = Assert.Single(open);
        Assert.Equal("Sender B", remaining.Sender);
    }

    [Fact]
    public void RecordReceivedMessage_ReplayingSameTelegramMessageId_IsSafeNoOp()
    {
        var store = new SqliteConversationStore(dbPath);
        store.RecordSentMessage("Build Script", "deploy-1", 100, "Q");

        store.RecordReceivedMessage("Build Script", "deploy-1", 101, "A", false);
        var exception = Record.Exception(() => store.RecordReceivedMessage("Build Script", "deploy-1", 101, "A", false));

        Assert.Null(exception);
        Assert.Equal(2, store.GetHistory("Build Script", "deploy-1", null).Count);
    }

    [Fact]
    public void LastUpdateId_RoundTrips()
    {
        var store = new SqliteConversationStore(dbPath);

        Assert.Null(store.GetLastUpdateId());

        store.SetLastUpdateId(42);

        Assert.Equal(42, store.GetLastUpdateId());
    }
}
