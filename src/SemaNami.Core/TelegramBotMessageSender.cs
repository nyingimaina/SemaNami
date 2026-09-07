using Telegram.Bot;
using Telegram.Bot.Types;

namespace SemaNami.Core;

// Thin wrapper over Telegram.Bot's ITelegramBotClient — kept free of branching logic so it
// doesn't need its own unit tests; Notifier's/ConversationSender's tests cover the contract via a mock.
public sealed class TelegramBotMessageSender : ITelegramMessageSender
{
    private readonly ITelegramBotClient client;

    public TelegramBotMessageSender(string botToken)
    {
        client = new TelegramBotClient(botToken);
    }

    public async Task SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default)
    {
        await client.SendMessage(chatId, text, cancellationToken: cancellationToken);
    }

    public async Task<int> SendReplyAsync(string chatId, string text, int? replyToMessageId, CancellationToken cancellationToken = default)
    {
        ReplyParameters? replyParameters = replyToMessageId.HasValue ? (ReplyParameters)replyToMessageId.Value : null;
        var sent = await client.SendMessage(chatId, text, replyParameters: replyParameters, cancellationToken: cancellationToken);
        return sent.MessageId;
    }
}
