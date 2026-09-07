using Moq;
using SemaNami.Core.Conversations;
using Xunit;

namespace SemaNami.Tests.Conversations;

public class ConversationWaitResolverTests : IDisposable
{
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

    [Fact]
    public async Task ResolveAsync_MessageAlreadyInStore_ReturnsImmediatelyWithoutSubscribing()
    {
        var store = CreateStore();
        store.RecordReceivedMessage("Build Script", "deploy-1", 100, "Already here", false);
        var resolver = new ConversationWaitResolver(store, realtimeNotifier.Object);

        var result = await resolver.ResolveAsync("Build Script", "deploy-1", afterSeq: null, CancellationToken.None);

        Assert.Single(result);
        realtimeNotifier.Verify(
            n => n.SubscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_NothingYetInStore_SubscribesAndReturnsWhatThePublishDelivers()
    {
        var store = CreateStore();
        var message = new StoredMessage(1, "received", 100, "Just arrived", false, DateTime.UtcNow);
        realtimeNotifier
            .Setup(n => n.SubscribeAsync("Build Script", "deploy-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var resolver = new ConversationWaitResolver(store, realtimeNotifier.Object);

        var result = await resolver.ResolveAsync("Build Script", "deploy-1", afterSeq: null, CancellationToken.None);

        Assert.Equal(message, Assert.Single(result));
    }

    [Fact]
    public async Task ResolveAsync_SubscriptionTimesOut_ReturnsEmpty()
    {
        var store = CreateStore();
        realtimeNotifier
            .Setup(n => n.SubscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoredMessage?)null);
        var resolver = new ConversationWaitResolver(store, realtimeNotifier.Object);

        var result = await resolver.ResolveAsync("Build Script", "deploy-1", afterSeq: null, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ResolveAsync_RespectsAfterSeq_OnlyTreatsNewerMessagesAsAlreadyAvailable()
    {
        var store = CreateStore();
        store.RecordReceivedMessage("Build Script", "deploy-1", 100, "Old", false);
        var afterSeq = store.GetHistory("Build Script", "deploy-1", null)[0].Seq;
        realtimeNotifier
            .Setup(n => n.SubscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoredMessage?)null);
        var resolver = new ConversationWaitResolver(store, realtimeNotifier.Object);

        // Nothing newer than afterSeq exists yet, so this must fall through to subscribing
        // rather than incorrectly returning the old message.
        await resolver.ResolveAsync("Build Script", "deploy-1", afterSeq, CancellationToken.None);

        realtimeNotifier.Verify(
            n => n.SubscribeAsync("Build Script", "deploy-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
