namespace SemaNami.Core.Conversations;

// Pure in-process pub/sub, no I/O — what ConversationListener publishes to right after a
// message is durably persisted, and what PipeSubscriptionServer subscribes to on behalf of a
// waiting `--wait-for-reply` client. Never a substitute for IConversationStore: if nobody is
// subscribed when PublishNewMessage fires, the message is still safely in the database.
public interface IRealtimeNotifier
{
    void PublishNewMessage(string sender, string conversationId, StoredMessage message);

    // Completes with the next published message for (sender, conversationId), or null if
    // cancellationToken is cancelled first (e.g. the caller's own wait timeout).
    Task<StoredMessage?> SubscribeAsync(string sender, string conversationId, CancellationToken cancellationToken);
}
