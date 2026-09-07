namespace SemaNami.Core.Conversations;

public interface IConversationStore
{
    Conversation? GetConversation(string sender, string conversationId);

    void RecordSentMessage(string sender, string conversationId, int telegramMessageId, string text);

    // Searches across all senders by design — the listener has no way to know in advance which
    // sender an incoming Telegram reply belongs to until it looks up the message it replied to.
    (string Sender, string ConversationId)? TryFindConversationByMessageId(int telegramMessageId);

    // Global, across all senders by design — see ConversationListener's fallback-matching logic.
    IReadOnlyList<Conversation> GetOpenConversations();

    // Returns the recorded row (or the pre-existing one, if this telegramMessageId was already
    // processed — see the UNIQUE index on messages.telegram_message_id) so callers like
    // ConversationListener can publish it to IRealtimeNotifier without a second query.
    StoredMessage RecordReceivedMessage(string sender, string conversationId, int telegramMessageId, string text, bool ambiguousMatch);

    void CloseConversation(string sender, string conversationId);

    IReadOnlyList<StoredMessage> GetHistory(string sender, string conversationId, long? afterSeq);

    // Global — there is only one Telegram update stream, not one per sender.
    int? GetLastUpdateId();

    void SetLastUpdateId(int updateId);
}
