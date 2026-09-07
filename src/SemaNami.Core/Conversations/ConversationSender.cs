namespace SemaNami.Core.Conversations;

// Used by the CLI's -conversation send path: looks up or implicitly creates the conversation
// (via the store's upsert-on-write), replies to whichever message it last sent/received in this
// thread if one exists, and records the outgoing message. Returns immediately — never blocks on
// a reply.
public sealed class ConversationSender
{
    private readonly IConversationStore store;
    private readonly ITelegramMessageSender messageSender;
    private readonly string chatId;

    public ConversationSender(IConversationStore store, ITelegramMessageSender messageSender, string chatId)
    {
        this.store = store;
        this.messageSender = messageSender;
        this.chatId = chatId;
    }

    public async Task SendAsync(string sender, string conversationId, string message, CancellationToken cancellationToken = default)
    {
        var existing = store.GetConversation(sender, conversationId);
        var replyToMessageId = existing?.LastMessageId;

        var telegramMessageId = await messageSender.SendReplyAsync(chatId, message, replyToMessageId, cancellationToken);

        store.RecordSentMessage(sender, conversationId, telegramMessageId, message);
    }
}
