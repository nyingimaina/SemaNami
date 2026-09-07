using SemaNami.Core.Conversations;
using Xunit;

namespace SemaNami.Tests.Conversations;

public class InProcessRealtimeNotifierTests
{
    private static StoredMessage MakeMessage(int telegramMessageId = 1) =>
        new(1, "received", telegramMessageId, "hi", false, DateTime.UtcNow);

    [Fact]
    public async Task SubscribeThenPublish_ResolvesWithThePublishedMessage()
    {
        var notifier = new InProcessRealtimeNotifier();
        var waitTask = notifier.SubscribeAsync("Sender A", "conv1", CancellationToken.None);

        var message = MakeMessage(42);
        notifier.PublishNewMessage("Sender A", "conv1", message);

        var result = await waitTask;
        Assert.Equal(message, result);
    }

    [Fact]
    public void Publish_WithNoSubscriber_IsSafeNoOp()
    {
        var notifier = new InProcessRealtimeNotifier();

        var exception = Record.Exception(() => notifier.PublishNewMessage("Nobody", "nothing", MakeMessage()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Publish_WithMultipleSubscribersOnSameKey_ResolvesAll()
    {
        var notifier = new InProcessRealtimeNotifier();
        var wait1 = notifier.SubscribeAsync("Sender A", "conv1", CancellationToken.None);
        var wait2 = notifier.SubscribeAsync("Sender A", "conv1", CancellationToken.None);

        var message = MakeMessage(7);
        notifier.PublishNewMessage("Sender A", "conv1", message);

        Assert.Equal(message, await wait1);
        Assert.Equal(message, await wait2);
    }

    [Fact]
    public async Task Publish_ForDifferentConversation_DoesNotResolveUnrelatedWaiter()
    {
        var notifier = new InProcessRealtimeNotifier();
        var waitTask = notifier.SubscribeAsync("Sender A", "conv1", CancellationToken.None);

        notifier.PublishNewMessage("Sender A", "conv2", MakeMessage());
        notifier.PublishNewMessage("Sender B", "conv1", MakeMessage());

        var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromMilliseconds(100)));

        Assert.NotSame(waitTask, completed);
        Assert.False(waitTask.IsCompleted);
    }

    [Fact]
    public async Task SubscribeAsync_CancelledBeforePublish_ReturnsNull()
    {
        var notifier = new InProcessRealtimeNotifier();
        using var cts = new CancellationTokenSource();

        var waitTask = notifier.SubscribeAsync("Sender A", "conv1", cts.Token);
        cts.Cancel();

        var result = await waitTask;
        Assert.Null(result);
    }
}
