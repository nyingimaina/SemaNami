namespace SemaNami.Core.Conversations;

// Sender/conversation identity a non-reply message is attributed to when there is nowhere
// better to put it. Shared and sender-less by design (see IConversationStore's doc comments).
public static class UnmatchedConversation
{
    public const string Sender = "_system";
    public const string ConversationId = "_unmatched";
}

public sealed class ConversationListener
{
    private const int LongPollTimeoutSeconds = 30;

    private readonly IConversationStore store;
    private readonly IUpdatesSource updatesSource;
    private readonly IRealtimeNotifier realtimeNotifier;

    public ConversationListener(IConversationStore store, IUpdatesSource updatesSource, IRealtimeNotifier realtimeNotifier)
    {
        this.store = store;
        this.updatesSource = updatesSource;
        this.realtimeNotifier = realtimeNotifier;
    }

    // One poll-and-process cycle. Returns the number of updates processed. A thin loop
    // elsewhere just calls this repeatedly — all matching/persistence/notification logic lives
    // here so it's testable without a real timer or a real Telegram connection.
    public async Task<int> PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var offset = store.GetLastUpdateId();
        var updates = await updatesSource.GetUpdatesAsync(offset, LongPollTimeoutSeconds, cancellationToken);

        foreach (var update in updates)
        {
            ProcessUpdate(update);
            store.SetLastUpdateId(update.UpdateId + 1);
        }

        return updates.Count;
    }

    private void ProcessUpdate(IncomingUpdate update)
    {
        var (sender, conversationId, ambiguous) = ResolveConversation(update);

        // The rule, exactly as required: a message is only ever eligible to notify a realtime
        // waiter after it has been durably persisted — RecordReceivedMessage always runs first.
        var stored = store.RecordReceivedMessage(sender, conversationId, update.MessageId, update.Text, ambiguous);
        realtimeNotifier.PublishNewMessage(sender, conversationId, stored);
    }

    private (string Sender, string ConversationId, bool Ambiguous) ResolveConversation(IncomingUpdate update)
    {
        if (update.ReplyToMessageId.HasValue)
        {
            var matched = store.TryFindConversationByMessageId(update.ReplyToMessageId.Value);
            if (matched.HasValue)
            {
                return (matched.Value.Sender, matched.Value.ConversationId, false);
            }
        }

        // Fallback: the open-conversation set considered here is global, across all senders —
        // there is only one Telegram chat shared by every app using SemaNami, so the listener
        // has no way to know in advance which sender an un-threaded reply is "for."
        var open = store.GetOpenConversations();
        if (open.Count == 0)
        {
            return (UnmatchedConversation.Sender, UnmatchedConversation.ConversationId, false);
        }

        if (open.Count == 1)
        {
            return (open[0].Sender, open[0].ConversationId, false);
        }

        var mostRecent = open.OrderByDescending(c => c.UpdatedAtUtc).First();
        return (mostRecent.Sender, mostRecent.ConversationId, true);
    }
}
