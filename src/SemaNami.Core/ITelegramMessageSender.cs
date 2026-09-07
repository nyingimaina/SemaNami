namespace SemaNami.Core;

public interface ITelegramMessageSender
{
    Task SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default);

    // Additive — SendMessageAsync above is untouched. Returns the sent message's Telegram id so
    // ConversationSender can record it for later reply-correlation. replyToMessageId is null to
    // start a new thread, or the id of a message to visibly reply to in Telegram.
    Task<int> SendReplyAsync(string chatId, string text, int? replyToMessageId, CancellationToken cancellationToken = default);
}
