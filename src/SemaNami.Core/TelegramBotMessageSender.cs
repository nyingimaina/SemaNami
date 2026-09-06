using Telegram.Bot;

namespace SemaNami.Core;

// Thin wrapper over Telegram.Bot's ITelegramBotClient — kept free of branching logic so it
// doesn't need its own unit tests; Notifier's tests cover this interface's contract via a mock.
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
}
