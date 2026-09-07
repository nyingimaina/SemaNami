namespace SemaNami.Core.Conversations;

// The race-closing logic behind --wait-for-reply: a message could arrive between a client
// checking history and subscribing to be notified of the next one, so this always checks the
// durable store FIRST and only falls back to the realtime subscription if genuinely nothing new
// exists yet. Kept separate from PipeSubscriptionServer's actual pipe I/O so this is directly
// unit-testable without a real named pipe.
public sealed class ConversationWaitResolver
{
    private readonly IConversationStore store;
    private readonly IRealtimeNotifier realtimeNotifier;

    public ConversationWaitResolver(IConversationStore store, IRealtimeNotifier realtimeNotifier)
    {
        this.store = store;
        this.realtimeNotifier = realtimeNotifier;
    }

    public async Task<IReadOnlyList<StoredMessage>> ResolveAsync(string sender, string conversationId, long? afterSeq, CancellationToken cancellationToken)
    {
        var existing = store.GetHistory(sender, conversationId, afterSeq);
        if (existing.Count > 0)
        {
            return existing;
        }

        var message = await realtimeNotifier.SubscribeAsync(sender, conversationId, cancellationToken);
        return message is null ? Array.Empty<StoredMessage>() : new[] { message };
    }
}
